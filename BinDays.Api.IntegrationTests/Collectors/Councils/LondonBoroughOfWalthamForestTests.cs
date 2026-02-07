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
	public async Task GetBinDaysTest(string postcode)
	{
		await TestSteps.EndToEnd(
			_client,
			postcode,
			"waltham-forest",
			_outputHelper
		);
	}
}
