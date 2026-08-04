namespace BinDays.Api.IntegrationTests.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Councils;
using BinDays.Api.IntegrationTests.Helpers;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class NewcastleUnderLymeTests
{
	private readonly IntegrationTestClient _client;
	private readonly ITestOutputHelper _outputHelper;
	private static readonly string _govUkId = new NewcastleUnderLyme().GovUkId;

	public NewcastleUnderLymeTests(ITestOutputHelper outputHelper)
	{
		_outputHelper = outputHelper;
		_client = new IntegrationTestClient(outputHelper);
	}

	[Theory]
	[InlineData("ST5 7LD", 1)]
	[InlineData("ST5 7LD", 0, "100031722272", 1)]
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
