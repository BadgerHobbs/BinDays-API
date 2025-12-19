namespace BinDays.Api.IntegrationTests.Collectors
{
	using BinDays.Api.Collectors.Collectors.Vendors;
	using BinDays.Api.Collectors.Exceptions;
	using BinDays.Api.Collectors.Services;
	using BinDays.Api.IntegrationTests.Helpers;
	using Xunit;
	using Xunit.Abstractions;

	public sealed class MultiAddressCollectorTests
	{
		private readonly IntegrationTestClient _client;
		private readonly CollectorService _collectorService = new([]);
		private const string _postcode = "SS9 3RE";

		public MultiAddressCollectorTests(ITestOutputHelper outputHelper)
		{
			_client = new IntegrationTestClient(outputHelper);
		}

		[Fact]
		public async Task GetCollectorTest()
		{
			await Assert.ThrowsAsync<UnsupportedCollectorException>(async () =>
			{
				await _client.ExecuteRequestCycleAsync(
					initialFunc: () => GovUkCollectorBase.GetCollector(_collectorService, _postcode, null),
					subsequentFunc: (csr) => GovUkCollectorBase.GetCollector(_collectorService, _postcode, csr),
					nextRequestExtractor: (resp) => resp.NextClientSideRequest,
					resultExtractor: (resp) => resp.Collector,
					errorMessage: $"Could not retrieve collector for postcode {_postcode}."
				);
			});
		}
	}
}
