namespace BinDays.Api.IntegrationTests.Helpers;

using BinDays.Api.Collectors.Models;
using System.Collections.Concurrent;
using System.Net;
using Xunit.Abstractions;

/// <summary>
/// Provides static helper methods for executing common integration test steps.
/// </summary>
internal static class TestSteps
{
	/// <summary>
	/// Caches resolved collectors by normalised postcode to avoid repeat gov.uk lookups within a test run.
	/// </summary>
	private static readonly ConcurrentDictionary<string, TestCollector> _collectorCache = new();

	/// <summary>
	/// Executes the full end-to-end test cycle by posting to the real API endpoints.
	/// </summary>
	/// <param name="client">The integration test client.</param>
	/// <param name="postcode">The postcode to search for.</param>
	/// <param name="expectedGovUkId">The expected GOV.UK ID of the collector.</param>
	/// <param name="outputHelper">The test output helper.</param>
	/// <param name="addressIndex">Optional zero-based index of the address to select. Defaults to 0 (first address).</param>
	/// <returns>A task that represents the asynchronous operation.</returns>
	public static async Task EndToEnd(
		IntegrationTestClient client,
		string postcode,
		string expectedGovUkId,
		ITestOutputHelper outputHelper,
		int addressIndex = 0)
	{
		await EndToEndAsync(
			client,
			postcode,
			expectedGovUkId,
			outputHelper,
			addressIndex,
			maxRetries: 6
		);
	}

	/// <summary>
	/// Executes the end-to-end test cycle, retrying up to <paramref name="retriesRemaining"/> times on failure.
	/// </summary>
	private static async Task EndToEndAsync(
		IntegrationTestClient client,
		string postcode,
		string expectedGovUkId,
		ITestOutputHelper outputHelper,
		int addressIndex,
		int maxRetries,
		int attempt = 0)
	{
		try
		{
			// Step 1: Get Collector
			var collector = await GetCollectorAsync(client, postcode, expectedGovUkId);

			// Step 2: Get Addresses
			var addresses = await GetAddressesAsync(client, expectedGovUkId, postcode);
			var selectedAddress = addresses.ElementAt(addressIndex);

			// Step 3: Get Bin Days
			var binDays = await GetBinDaysAsync(
				client,
				expectedGovUkId,
				postcode,
				selectedAddress.Uid!
			);

			// Step 4: Output Summary
			TestOutput.WriteTestSummary(
				outputHelper,
				collector,
				addresses,
				binDays
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
				maxRetries,
				attempt + 1
			);
		}
	}

	/// <summary>
	/// Executes Step 1: Get Collector via POST /collector?postcode=...
	/// </summary>
	private static async Task<TestCollector> GetCollectorAsync(
		IntegrationTestClient client,
		string postcode,
		string expectedGovUkId)
	{
		var cacheKey = postcode.ToUpperInvariant().Replace(" ", "");
		if (_collectorCache.TryGetValue(cacheKey, out var cached))
		{
			return cached;
		}

		var response = await client.ExecuteRequestCycleAsync<TestGetCollectorResponse>(
			$"/collector?postcode={postcode}",
			resp => resp.NextClientSideRequest
		);

		TestValidation.ValidateCollectorResult(response.Collector, expectedGovUkId);

		var collector = response.Collector!;
		_collectorCache[cacheKey] = collector;

		return collector;
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
	/// Executes Step 3: Get Bin Days via POST /{govUkId}/bin-days?postcode=...&amp;uid=...
	/// </summary>
	private static async Task<IReadOnlyCollection<BinDay>> GetBinDaysAsync(
		IntegrationTestClient client,
		string govUkId,
		string postcode,
		string uid)
	{
		var response = await client.ExecuteRequestCycleAsync<GetBinDaysResponse>(
			$"/{govUkId}/bin-days?postcode={postcode}&uid={uid}",
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
