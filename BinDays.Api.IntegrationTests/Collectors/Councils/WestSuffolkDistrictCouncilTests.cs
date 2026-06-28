namespace BinDays.Api.IntegrationTests.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Councils;
using BinDays.Api.IntegrationTests.Helpers;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class WestSuffolkDistrictCouncilTests
{
	private readonly IntegrationTestClient _client;
	private readonly ITestOutputHelper _outputHelper;
	private static readonly string _govUkId = new WestSuffolkDistrictCouncil().GovUkId;

	public WestSuffolkDistrictCouncilTests(ITestOutputHelper outputHelper)
	{
		_outputHelper = outputHelper;
		_client = new IntegrationTestClient(outputHelper);
	}

	[Theory]
	[InlineData("IP32 7LW", 0)]
	// Address index 2 has no garden waste (Brown) subscription, which reflows the
	// page text so the Green bin date wraps after the day-of-week. This previously
	// broke the parsing regex (issue #26).
	[InlineData("IP32 7LW", 2)]
	public async Task GetBinDaysTest(string postcode, int addressIndex)
	{
		await TestSteps.EndToEnd(
			_client,
			postcode,
			_govUkId,
			_outputHelper,
			addressIndex
		);
	}
}
