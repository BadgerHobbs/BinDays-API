namespace BinDays.Api.IntegrationTests.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Councils;
using BinDays.Api.IntegrationTests.Helpers;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class PlymouthCouncilTests
{
	private readonly IntegrationTestClient _client;
	private readonly ITestOutputHelper _outputHelper;
	private static readonly string _govUkId = new PlymouthCouncil().GovUkId;

	public PlymouthCouncilTests(ITestOutputHelper outputHelper)
	{
		_outputHelper = outputHelper;
		_client = new IntegrationTestClient(outputHelper);
	}

	[Theory]
	[InlineData("pl36ag")]
	[InlineData("pl67aa")]
	[InlineData("pl54qg")]
	[InlineData("pl47eq", 31)]
	[InlineData("pl36ag", 0, "100040471574", 1)]
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
