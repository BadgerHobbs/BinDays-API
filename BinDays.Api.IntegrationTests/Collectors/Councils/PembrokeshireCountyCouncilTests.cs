namespace BinDays.Api.IntegrationTests.Collectors.Councils
{
    using BinDays.Api.Collectors.Collectors;
    using BinDays.Api.Collectors.Collectors.Councils;
    using BinDays.Api.Collectors.Services;
    using BinDays.Api.IntegrationTests.Helpers;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;

    public class PembrokeshireCountyCouncilTests
    {
        private readonly IntegrationTestClient _client = new();
        private static readonly ICollector _collector = new PembrokeshireCountyCouncil();
        private readonly CollectorService _collectorService = new([_collector]);
        private readonly ITestOutputHelper _outputHelper;

        public PembrokeshireCountyCouncilTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Theory]
        [InlineData("SA71 4EL")]
        public async Task GetBinDaysTest(string postcode)
        {
            await TestSteps.EndToEnd(
                _client,
                _collectorService,
                _collector,
                postcode,
                _outputHelper
            );
        }
    }
}
