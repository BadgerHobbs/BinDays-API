namespace BinDays.Api.IntegrationTests.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Councils;
using BinDays.Api.IntegrationTests.Helpers;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class SouthNorfolkCouncilTests
{
	private readonly IntegrationTestClient _client;
	private readonly ITestOutputHelper _outputHelper;
	private static readonly string _govUkId = new SouthNorfolkCouncil().GovUkId;

	public SouthNorfolkCouncilTests(ITestOutputHelper outputHelper)
	{
		_outputHelper = outputHelper;
		_client = new IntegrationTestClient(outputHelper);
	}

	[Theory]
	[InlineData("NR18 0HQ")]
	[InlineData("NR8 5GS", 16)]
	[InlineData("IP21 4QU", 14)]
	[InlineData("NR18 0HQ", 0, "2630147509", 1)]
	public async Task GetBinDaysTest(string postcode, int addressIndex = 0, string? pinnedUid = null, int? pinnedVersion = null)
	{
		await TestSteps.EndToEnd(
			_client,
			postcode,
			_govUkId,
			_outputHelper,
			addressIndex,
			pinnedUid: pinnedUid,
			pinnedVersion: pinnedVersion
		);
	}
}
