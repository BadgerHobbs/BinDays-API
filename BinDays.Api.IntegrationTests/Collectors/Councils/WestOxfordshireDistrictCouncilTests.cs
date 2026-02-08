namespace BinDays.Api.IntegrationTests.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Councils;
using BinDays.Api.IntegrationTests.Helpers;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class WestOxfordshireDistrictCouncilTests
{
    private readonly IntegrationTestClient _client;
    private readonly ITestOutputHelper _outputHelper;
    private static readonly string _govUkId = new WestOxfordshireDistrictCouncil().GovUkId;

    public WestOxfordshireDistrictCouncilTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _client = new IntegrationTestClient(outputHelper);
    }

    [Theory]
    [InlineData("OX28 5BA")]
    public async Task GetBinDaysTest(string postcode)
    {
        await TestSteps.EndToEnd(
            _client,
            postcode,
            _govUkId,
            _outputHelper
        );
    }
}