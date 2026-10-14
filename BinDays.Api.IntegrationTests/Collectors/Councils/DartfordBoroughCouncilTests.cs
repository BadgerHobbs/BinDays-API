namespace BinDays.Api.IntegrationTests.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Councils;
using BinDays.Api.IntegrationTests.Helpers;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class DartfordBoroughCouncilTests
{
	private readonly IntegrationTestClient _client;
	private readonly ITestOutputHelper _outputHelper;
	private static readonly string _govUkId = new DartfordBoroughCouncil().GovUkId;

	public DartfordBoroughCouncilTests(ITestOutputHelper outputHelper)
	{
		_outputHelper = outputHelper;
		_client = new IntegrationTestClient(outputHelper);
	}

	[Theory]
	[InlineData("BR8 7PF")]
	[InlineData("BR8 7PF", "2 Suttons Cottages Clement Street Swanley Kent BR8 7PF ", 1)]
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
