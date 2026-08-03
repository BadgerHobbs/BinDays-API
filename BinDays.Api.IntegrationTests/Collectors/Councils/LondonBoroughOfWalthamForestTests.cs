namespace BinDays.Api.IntegrationTests.Collectors.Councils;

using BinDays.Api.IntegrationTests.Helpers;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class LondonBoroughOfWalthamForestTests
{
	private readonly IntegrationTestClient _client;
	private readonly ITestOutputHelper _outputHelper;

	public LondonBoroughOfWalthamForestTests(ITestOutputHelper outputHelper)
	{
		_outputHelper = outputHelper;
		_client = new IntegrationTestClient(outputHelper);
	}

	[Theory]
	[InlineData("E17 9HN")]
	[InlineData("E17 9HN", "100022580826;E05013903", 1)]
	public async Task GetBinDaysTest(string postcode, string? pinnedUid = null, int? pinnedVersion = null)
	{
		await TestSteps.EndToEnd(
			_client,
			postcode,
			"waltham-forest",
			_outputHelper,
			pinnedUid: pinnedUid,
			pinnedVersion: pinnedVersion
		);
	}
}
