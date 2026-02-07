namespace BinDays.Api.IntegrationTests.Helpers;

using BinDays.Api.Collectors.Models;
using Xunit.Abstractions;

/// <summary>
/// Provides static helper methods for executing common integration test steps.
/// </summary>
internal static class TestSteps
{
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
		int addressIndex = 0
	)
	{
		// Step 1: Get Collector
		var collector = await GetCollectorAsync(client, postcode, expectedGovUkId);

		// Step 2: Get Addresses
		var addresses = await GetAddressesAsync(client, expectedGovUkId, postcode);
		var selectedAddress = addresses.ElementAt(addressIndex);

		// Step 3: Get Bin Days
		var binDays = await GetBinDaysAsync(client, expectedGovUkId, postcode, selectedAddress.Uid!);

		// Step 4: Output Summary
		TestOutput.WriteTestSummary(outputHelper, collector, addresses, binDays);
	}

	/// <summary>
	/// Executes Step 1: Get Collector via POST /collector?postcode=...
	/// </summary>
	private static async Task<TestCollector> GetCollectorAsync(
		IntegrationTestClient client,
		string postcode,
		string expectedGovUkId)
	{
		var response = await client.ExecuteRequestCycleAsync<TestGetCollectorResponse>(
			$"/collector?postcode={postcode}",
			resp => resp.NextClientSideRequest
		);

		TestValidation.ValidateCollectorResult(response.Collector, expectedGovUkId);

		return response.Collector!;
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
