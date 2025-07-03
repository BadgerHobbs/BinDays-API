namespace BinDays.Api.IntegrationTests.Collectors.Councils
{
    using BinDays.Api.Collectors.Collectors;
    using BinDays.Api.Collectors.Collectors.Councils;
    using BinDays.Api.Collectors.Services;
    using BinDays.Api.IntegrationTests.Helpers;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;

    public class BuckinghamshireCouncilTests
    {
        private readonly IntegrationTestClient _client = new();
        private static readonly ICollector _collector = new BuckinghamshireCouncil();
        private readonly CollectorService _collectorService = new([_collector]);
        private const string _postcode = "HP22 5XA";

        private readonly ITestOutputHelper _outputHelper;

        public BuckinghamshireCouncilTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task GetBinDaysTest()
        {
            await TestSteps.EndToEnd(
                _client,
                _collectorService,
                _collector,
                _postcode,
                _outputHelper
            );
        }
    }
}
