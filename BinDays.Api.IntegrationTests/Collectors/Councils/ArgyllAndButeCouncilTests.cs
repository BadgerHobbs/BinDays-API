namespace BinDays.Api.IntegrationTests.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Councils;
using BinDays.Api.IntegrationTests.Helpers;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class ArgyllAndButeCouncilTests
{
	private readonly IntegrationTestClient _client;
	private readonly ITestOutputHelper _outputHelper;
	private static readonly string _govUkId = new ArgyllAndButeCouncil().GovUkId;

	public ArgyllAndButeCouncilTests(ITestOutputHelper outputHelper)
	{
		_outputHelper = outputHelper;
		_client = new IntegrationTestClient(outputHelper);
	}

	[Theory]
	[InlineData("PA78 6SY")]  // Coll
	[InlineData("PA61 7YR")]  // Colonsay
	[InlineData("PA23 7AA")]  // Dunoon and Cowal
	[InlineData("G84 7AA")]   // Helensburgh and Lomond (blue, grey glass, food waste, green)
	[InlineData("PA42 7DA")]  // Islay and Jura
	[InlineData("PA20 9HP")]  // Isle of Bute (Rothesay)
	[InlineData("PA28 6RE")]  // Kintyre
	[InlineData("PA34 5UL")]  // Lismore
	[InlineData("PA31 8QY")]  // Mid Argyll and Tarbert
	[InlineData("PA73 6LT")]  // Mull and Iona (blue, grey glass, green)
	[InlineData("PA34 5PX")]  // Oban and Lorn
	[InlineData("PA77 6XA")]  // Tiree (blue, grey glass, green)
	[InlineData("PA78 6SY", "000125008375", 1)]
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
