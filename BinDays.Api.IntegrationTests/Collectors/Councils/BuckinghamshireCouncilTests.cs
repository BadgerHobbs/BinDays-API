namespace BinDays.Api.IntegrationTests.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Councils;
using BinDays.Api.IntegrationTests.Helpers;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class BuckinghamshireCouncilTests
{
	private readonly IntegrationTestClient _client;
	private readonly ITestOutputHelper _outputHelper;
	private static readonly string _govUkId = new BuckinghamshireCouncil().GovUkId;

	public BuckinghamshireCouncilTests(ITestOutputHelper outputHelper)
	{
		_outputHelper = outputHelper;
		_client = new IntegrationTestClient(outputHelper);
	}

	[Theory]
	[InlineData("HP22 5XA")] // Aylesbury Vale
	[InlineData("HP13 5AW")] // Wycombe
	[InlineData("HP9 1BG")]  // South Bucks
	[InlineData("HP7 0NQ")]  // Chiltern
	[InlineData("HP22 5XA", "766352432", 1)]
	// Aylesbury Vale sack property, which the council reports as "Refuse Sacks" and
	// "RECYCLING SACKS" rather than the wheeled-bin service names used elsewhere in the north.
	[InlineData("HP20 2RA", "766304863", 1)]
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
