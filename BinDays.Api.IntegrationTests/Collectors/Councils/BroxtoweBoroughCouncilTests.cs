namespace BinDays.Api.IntegrationTests.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Councils;
using BinDays.Api.IntegrationTests.Helpers;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class BroxtoweBoroughCouncilTests
{
	private readonly IntegrationTestClient _client;
	private readonly ITestOutputHelper _outputHelper;
	private static readonly string _govUkId = new BroxtoweBoroughCouncil().GovUkId;

	public BroxtoweBoroughCouncilTests(ITestOutputHelper outputHelper)
	{
		_outputHelper = outputHelper;
		_client = new IntegrationTestClient(outputHelper);
	}

	[Theory]
	[InlineData("NG16 2NB", 1)]
	[InlineData("NG16 2NB", 0, "U100031330592", 2)]
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
