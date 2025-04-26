namespace BinDays.Api.IntegrationTests.Helpers
{
	using BinDays.Api.Collectors.Collectors;
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Services;

	/// <summary>
	/// Provides static helper methods for executing common integration test steps.
	/// </summary>
	internal static class TestSteps
	{
		/// <summary>
		/// Executes Step 1: Get Collector.
		/// </summary>
		/// <param name="client">The integration test client.</param>
		/// <param name="collectorService">The collector service.</param>
		/// <param name="postcode">The postcode to search for.</param>
		/// <param name="expectedCollectorType">The expected concrete Type of the collector.</param>
		/// <param name="expectedGovUkId">The expected GOV.UK ID of the collector.</param>
		/// <returns>The found ICollector.</returns>
		public static async Task<ICollector> GetCollectorAsync(
			IntegrationTestClient client,
			CollectorService collectorService,
			string postcode,
			Type expectedCollectorType,
			string expectedGovUkId)
		{
			var collector = await client.ExecuteRequestCycleAsync(
				initialFunc: () => GovUkCollectorBase.GetCollector(collectorService, postcode, null),
				subsequentFunc: (csr) => GovUkCollectorBase.GetCollector(collectorService, postcode, csr),
				nextRequestExtractor: (resp) => resp.NextClientSideRequest,
				resultExtractor: (resp) => resp.Collector,
				errorMessage: $"Could not retrieve collector for postcode {postcode}."
			);

			// Use the validation helper
			TestValidation.ValidateCollectorResult(
				collector,
				expectedCollectorType,
				expectedGovUkId
			);

			return collector;
		}

		/// <summary>
		/// Executes Step 2: Get Addresses.
		/// </summary>
		/// <param name="client">The integration test client.</param>
		/// <param name="collector">The collector instance.</param>
		/// <param name="postcode">The postcode to search for.</param>
		/// <returns>The read-only collection of found addresses.</returns>
		public static async Task<IReadOnlyCollection<Address>> GetAddressesAsync(
			IntegrationTestClient client,
			ICollector collector,
			string postcode)
		{
			var addresses = await client.ExecuteRequestCycleAsync(
				initialFunc: () => collector.GetAddresses(postcode, null),
				subsequentFunc: (csr) => collector.GetAddresses(postcode, csr),
				nextRequestExtractor: (resp) => resp.NextClientSideRequest,
				resultExtractor: (resp) => resp.Addresses,
				errorMessage: $"Could not retrieve addresses for postcode {postcode} from {collector.Name}."
			);

			// Use the validation helper
			TestValidation.ValidateAddressesResult(
				addresses,
				expectedPostcode: postcode,
				ensureUidPresent: true
			);

			return addresses;
		}

		/// <summary>
		/// Executes Step 3: Get Bin Days.
		/// </summary>
		/// <param name="client">The integration test client.</param>
		/// <param name="collector">The collector instance.</param>
		/// <param name="address">The specific address to get bin days for.</param>
		/// <returns>The read-only collection of found bin days.</returns>
		public static async Task<IReadOnlyCollection<BinDay>> GetBinDaysAsync(
			IntegrationTestClient client,
			ICollector collector,
			Address address)
		{
			var binDays = await client.ExecuteRequestCycleAsync(
				initialFunc: () => collector.GetBinDays(address, null),
				subsequentFunc: (csr) => collector.GetBinDays(address, csr),
				nextRequestExtractor: (resp) => resp.NextClientSideRequest,
				resultExtractor: (resp) => resp.BinDays,
				errorMessage: $"Could not retrieve bin days for address Uid {address.Uid} from {collector.Name}."
			);

			// Use the validation helper
			TestValidation.ValidateBinDaysResult(
				binDays,
				ensureBinsPresent: true,
				ensureFutureDates: true
			);

			return binDays;
		}
	}
}
