namespace BinDays.Api.IntegrationTests.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Councils;
using BinDays.Api.IntegrationTests.Helpers;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class MiddlesbroughBoroughCouncilTests
{
	private readonly IntegrationTestClient _client;
	private readonly ITestOutputHelper _outputHelper;
	private static readonly string _govUkId = new MiddlesbroughBoroughCouncil().GovUkId;

	public MiddlesbroughBoroughCouncilTests(ITestOutputHelper outputHelper)
	{
		_outputHelper = outputHelper;
		_client = new IntegrationTestClient(outputHelper);
	}

	[Theory]
	[InlineData("TS1 1AA")]
	[InlineData("TS1 1AA", "3EB7B752-C02F-11F0-A1CC-A853A45D7B8A", 1)]
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
