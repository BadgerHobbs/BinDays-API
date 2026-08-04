namespace BinDays.Api.IntegrationTests.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Councils;
using BinDays.Api.IntegrationTests.Helpers;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class RochfordDistrictCouncilTests
{
	private readonly IntegrationTestClient _client;
	private readonly ITestOutputHelper _outputHelper;
	private static readonly string _govUkId = new RochfordDistrictCouncil().GovUkId;

	public RochfordDistrictCouncilTests(ITestOutputHelper outputHelper)
	{
		_outputHelper = outputHelper;
		_client = new IntegrationTestClient(outputHelper);
	}

	[Theory]
	[InlineData("SS6 9GU")]
	[InlineData("SS6 9GU", "E05010844-100090579593;form-dWYGQ45RMYEWtSag-DmxEohEPCgXH7oGnm_MNmw0XZ8", 1)]
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
