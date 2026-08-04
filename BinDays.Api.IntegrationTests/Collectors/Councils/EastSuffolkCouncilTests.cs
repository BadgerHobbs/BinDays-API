namespace BinDays.Api.IntegrationTests.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Councils;
using BinDays.Api.IntegrationTests.Helpers;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class EastSuffolkCouncilTests
{
	private readonly IntegrationTestClient _client;
	private readonly ITestOutputHelper _outputHelper;
	private static readonly string _govUkId = new EastSuffolkCouncil().GovUkId;

	public EastSuffolkCouncilTests(ITestOutputHelper outputHelper)
	{
		_outputHelper = outputHelper;
		_client = new IntegrationTestClient(outputHelper);
	}

	[Theory]
	[InlineData("IP3 8UE", 5)]
	[InlineData("IP3 8UE", 0, "100091393218", 1)]
	public async Task GetBinDaysTest(string postcode, int addressIndex, string? pinnedUid = null, int? pinnedVersion = null)
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
