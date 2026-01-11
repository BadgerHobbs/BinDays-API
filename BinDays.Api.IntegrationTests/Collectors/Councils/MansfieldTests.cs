namespace BinDays.Api.IntegrationTests.Collectors.Councils;

using BinDays.Api.Collectors.Collectors;
using BinDays.Api.Collectors.Collectors.Councils;
using BinDays.Api.Collectors.Services;
using BinDays.Api.IntegrationTests.Helpers;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class MansfieldTests
{
	private readonly IntegrationTestClient _client;
	private static readonly ICollector _collector = new Mansfield();
	private readonly CollectorService _collectorService = new([_collector]);
	private readonly ITestOutputHelper _outputHelper;

	public MansfieldTests(ITestOutputHelper outputHelper)
	{
		_outputHelper = outputHelper;
		_client = new IntegrationTestClient(outputHelper);
	}

	[Theory]
	[InlineData("NG19 8PL")]
	public async Task GetBinDaysTest(string postcode)
	{
		await TestSteps.EndToEnd(
			_client,
			_collectorService,
			_collector,
			postcode,
			_outputHelper
		);
	}
}
