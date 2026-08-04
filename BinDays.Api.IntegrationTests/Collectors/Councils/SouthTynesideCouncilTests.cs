namespace BinDays.Api.IntegrationTests.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Councils;
using BinDays.Api.IntegrationTests.Helpers;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class SouthTynesideCouncilTests
{
	private readonly IntegrationTestClient _client;
	private readonly ITestOutputHelper _outputHelper;
	private static readonly string _govUkId = new SouthTynesideCouncil().GovUkId;

	public SouthTynesideCouncilTests(ITestOutputHelper outputHelper)
	{
		_outputHelper = outputHelper;
		_client = new IntegrationTestClient(outputHelper);
	}

	[Theory]
	[InlineData("NE34 0SY")]
	[InlineData("NE34 0SY", "S100000345869;1 PAGE AVENUE, SOUTH SHIELDS, SOUTH TYNESIDE, TYNE AND WEAR, NE34 0SY", 1)]
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
