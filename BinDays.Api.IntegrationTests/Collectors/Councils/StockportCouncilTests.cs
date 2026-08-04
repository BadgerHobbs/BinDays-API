namespace BinDays.Api.IntegrationTests.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Councils;
using BinDays.Api.IntegrationTests.Helpers;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class StockportCouncilTests
{
	private readonly IntegrationTestClient _client;
	private readonly ITestOutputHelper _outputHelper;
	private static readonly string _govUkId = new StockportCouncil().GovUkId;

	public StockportCouncilTests(ITestOutputHelper outputHelper)
	{
		_outputHelper = outputHelper;
		_client = new IntegrationTestClient(outputHelper);
	}

	[Theory]
	[InlineData("SK5 6DZ")]
	[InlineData("SK5 6DZ", "101002278607|1 LIVERPOOL CLOSE, NORTH REDDISH, STOCKPORT, STOCKPORT, SK5 6DZ", 1)]
	public async Task GetBinDaysTest(string postcode, string? pinnedUid = null, int? pinnedVersion = null)
	{
		await TestSteps.EndToEnd(
			_client,
			postcode,
			_govUkId,
			_outputHelper,
			pinnedUid: pinnedUid,
			pinnedVersion: pinnedVersion
		);
	}
}
