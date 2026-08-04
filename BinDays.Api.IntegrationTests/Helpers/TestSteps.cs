namespace BinDays.Api.IntegrationTests.Helpers;

using BinDays.Api.Collectors.Models;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Provides static helper methods for executing common integration test steps.
/// </summary>
internal static class TestSteps
{
	/// <summary>
	/// Caches resolved collectors by gov.uk ID to avoid repeat <c>/collectors</c> calls within a test run.
	/// </summary>
	private static readonly ConcurrentDictionary<string, TestCollector> _collectorCache = new();

	/// <summary>
	/// Executes the full end-to-end test cycle by posting to the real API endpoints.
	/// </summary>
	/// <param name="client">The integration test client.</param>
	/// <param name="postcode">The postcode to search for.</param>
	/// <param name="expectedGovUkId">The expected GOV.UK ID of the collector.</param>
	/// <param name="outputHelper">The test output helper.</param>
	/// <param name="addressIndex">Optional zero-based index of the address to select. Defaults to 0 (first address). Ignored when <paramref name="pinnedUid"/> is provided.</param>
	/// <param name="pinnedUid">Optional Uid pinned from an earlier search. When provided it is used instead of selecting an address from the freshly fetched list, simulating a user who selected an address a while ago and hasn't reopened the address picker since. Must be provided together with <paramref name="pinnedVersion"/>.</param>
	/// <param name="pinnedVersion">The collector version at the time <paramref name="pinnedUid"/> was pinned. Must stay a hardcoded literal, never read dynamically from the current collector, so a future version bump is actually exercised by the test. A 410 Gone response fails the test: it means the collector's Version has been bumped since the Uid was pinned, invalidating every saved address for that collector. Must be provided together with <paramref name="pinnedUid"/>.</param>
	/// <returns>A task that represents the asynchronous operation.</returns>
	public static async Task EndToEnd(
		IntegrationTestClient client,
		string postcode,
		string expectedGovUkId,
		ITestOutputHelper outputHelper,
		int addressIndex = 0,
		string? pinnedUid = null,
		int? pinnedVersion = null)
	{
		if (pinnedUid is null ^ pinnedVersion is null)
		{
			throw new ArgumentException(
				$"{nameof(pinnedUid)} and {nameof(pinnedVersion)} must both be provided together, or both omitted."
			);
		}

		if (pinnedUid is not null && addressIndex != 0)
		{
			throw new ArgumentException(
				$"{nameof(addressIndex)} is ignored when {nameof(pinnedUid)} is provided, since the pinned Uid " +
				$"already identifies the address. Pass 0 to avoid implying otherwise."
			);
		}

		await EndToEndAsync(
			client,
			postcode,
			expectedGovUkId,
			outputHelper,
			addressIndex,
			pinnedUid,
			pinnedVersion,
			maxRetries: 6
		);
	}

	/// <summary>
	/// Executes the end-to-end test cycle, retrying up to <paramref name="maxRetries"/> times on failure.
	/// </summary>
	private static async Task EndToEndAsync(
		IntegrationTestClient client,
		string postcode,
		string expectedGovUkId,
		ITestOutputHelper outputHelper,
		int addressIndex,
		string? pinnedUid,
		int? pinnedVersion,
		int maxRetries,
		int attempt = 0)
	{
		try
		{
			// Step 1: Get Collector. Resolved via /collectors rather than the postcode-driven
			// /collector lookup, since the latter makes a real client-side request to gov.uk
			// and every council test resolving its own postcode against the live site on every
			// run gets rate-limited. The gov.uk parsing logic itself is covered by
			// GovUkIdNotFoundTests against mocked responses instead.
			var collector = await GetCollectorAsync(client, expectedGovUkId);

			// Step 2: Get Addresses. Still fetched for a pinned run — it is cached server-side, and
			// keeps the real address list in the test summary for diagnosing a failure.
			var addresses = await GetAddressesAsync(client, expectedGovUkId, postcode);

			// A pinned Uid stands in for the address the user selected earlier, instead of
			// re-picking one from the list that was just fetched.
			var uid = pinnedUid ?? addresses.ElementAt(addressIndex).Uid!;

			// Step 3: Get Bin Days. A pinned version is sent as-is rather than the collector's
			// current Version, so a version bump since the Uid was pinned surfaces as a 410.
			var binDays = await GetBinDaysAsync(
				client,
				expectedGovUkId,
				postcode,
				uid,
				pinnedVersion ?? collector.Version
			);

			// Step 4: Output Summary
			TestOutput.WriteTestSummary(
				outputHelper,
				collector,
				addresses,
				binDays
			);
		}
		catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Gone && pinnedVersion.HasValue)
		{
			// 410 Gone means the collector's Version has been bumped since this Uid was pinned, so
			// every previously-saved address for this collector is now rejected and each affected
			// user must manually re-select theirs in the app. The API is behaving correctly, but
			// that is still a real user-facing break, so surface it as a failure rather than a pass.
			Assert.Fail(
				$"Pinned version {pinnedVersion} is no longer accepted (410 Gone). The collector's Version has been " +
				"bumped since this Uid was pinned, so every saved address for this collector is now invalid and " +
				"affected users must re-select their address in the app. If that break was intended, re-capture this " +
				"pin against the current version."
			);
		}
		catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests && attempt < maxRetries)
		{
			// 409 Conflict: retry with exponential backoff
			var delay = TimeSpan.FromSeconds(5 * (int)Math.Pow(2, attempt));
			outputHelper.WriteLine($"[Retry {attempt + 1}/{maxRetries}] 409 Conflict — backing off for {delay.TotalSeconds}s. {ex.Message}");
			await Task.Delay(delay);
			await EndToEndAsync(
				client,
				postcode,
				expectedGovUkId,
				outputHelper,
				addressIndex,
				pinnedUid,
				pinnedVersion,
				maxRetries,
				attempt + 1
			);
		}
		catch (Exception ex) when (attempt < 1)
		{
			// General exception: retry once only for flaky tests
			outputHelper.WriteLine($"[Retry 1/1] {ex.Message}");
			await EndToEndAsync(
				client,
				postcode,
				expectedGovUkId,
				outputHelper,
				addressIndex,
				pinnedUid,
				pinnedVersion,
				maxRetries,
				attempt + 1
			);
		}
	}

	/// <summary>
	/// Executes Step 1: Get Collector via GET /collectors, picking out the entry matching
	/// <paramref name="expectedGovUkId"/>. Avoids the postcode-driven /collector endpoint, which
	/// makes a real client-side request to gov.uk and would otherwise be exercised once per
	/// council test.
	/// </summary>
	private static async Task<TestCollector> GetCollectorAsync(
		IntegrationTestClient client,
		string expectedGovUkId)
	{
		if (_collectorCache.TryGetValue(expectedGovUkId, out var cached))
		{
			return cached;
		}

		var collectors = await client.GetAsync<List<TestCollector>>("/collectors");
		var collector = collectors.SingleOrDefault(c => c.GovUkId == expectedGovUkId);

		TestValidation.ValidateCollectorResult(collector, expectedGovUkId);

		_collectorCache[expectedGovUkId] = collector!;

		return collector!;
	}

	/// <summary>
	/// Executes Step 2: Get Addresses via POST /{govUkId}/addresses?postcode=...
	/// </summary>
	private static async Task<IReadOnlyCollection<Address>> GetAddressesAsync(
		IntegrationTestClient client,
		string govUkId,
		string postcode)
	{
		var response = await client.ExecuteRequestCycleAsync<GetAddressesResponse>(
			$"/{govUkId}/addresses?postcode={postcode}",
			resp => resp.NextClientSideRequest
		);

		TestValidation.ValidateAddressesResult(response.Addresses, ensureUidPresent: true);

		return response.Addresses!;
	}

	/// <summary>
	/// Executes Step 3: Get Bin Days via POST /{govUkId}/bin-days?postcode=...&amp;uid=...&amp;version=...
	/// </summary>
	private static async Task<IReadOnlyCollection<BinDay>> GetBinDaysAsync(
		IntegrationTestClient client,
		string govUkId,
		string postcode,
		string uid,
		int version)
	{
		var response = await client.ExecuteRequestCycleAsync<GetBinDaysResponse>(
			$"/{govUkId}/bin-days?postcode={postcode}&uid={uid}&version={version}",
			resp => resp.NextClientSideRequest
		);

		TestValidation.ValidateBinDaysResult(
			response.BinDays,
			ensureBinsPresent: true,
			ensureFutureDates: true,
			ensureSortedByDate: true
		);

		return response.BinDays!;
	}
}
