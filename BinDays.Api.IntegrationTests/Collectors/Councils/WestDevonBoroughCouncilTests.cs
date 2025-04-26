namespace BinDays.Api.IntegrationTests.Collectors.Councils
{
    using BinDays.Api.Collectors.Collectors.Councils;
    using BinDays.Api.IntegrationTests.Helpers;
    using BinDays.Api.Collectors.Services;
	using BinDays.Api.Collectors.Collectors;

	[TestClass]
    public class WestDevonBoroughCouncilTests
    {
        private readonly IntegrationTestClient _client = new();
        private static readonly ICollector _collector = new WestDevonBoroughCouncil();
        private readonly CollectorService _collectorService = new([_collector]);
        private const string _postcode = "EX20 1ZF";

        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public async Task GetBinDaysTest()
        {
            // Step 1: Get Collector
            var collector = await TestSteps.GetCollectorAsync(
                _client,
                _collectorService,
                _postcode,
                _collector.GetType(),
                _collector.GovUkId
			);

            // Step 2: Get Addresses
            var addresses = await TestSteps.GetAddressesAsync(
                _client,
                collector,
                _postcode
			);

            var selectedAddress = addresses.First();

            // Step 3: Get Bin Days
            var binDays = await TestSteps.GetBinDaysAsync(
                _client,
                collector,
                selectedAddress
			);

            // Step 4: Output Summary
            TestOutput.WriteTestSummary(
                this.TestContext,
                collector,
                addresses,
                binDays
			);
        }
    }
}
