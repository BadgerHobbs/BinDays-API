namespace BinDays.Api.IntegrationTests.Collectors.Councils;

using BinDays.Api.Collectors.Collectors;
using BinDays.Api.Collectors.Collectors.Councils;
using BinDays.Api.Collectors.Services;
using BinDays.Api.IntegrationTests.Helpers;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class BradfordCouncilTests
{
	private readonly IntegrationTestClient _client;
	private static readonly ICollector _collector = new BradfordCouncil();
	private readonly CollectorService _collectorService = new([_collector]);
	private readonly ITestOutputHelper _outputHelper;

	public BradfordCouncilTests(ITestOutputHelper outputHelper)
	{
		_outputHelper = outputHelper;
		_client = new IntegrationTestClient(outputHelper);
	}

	[Theory]
	[InlineData("BD5 9ND")]
	public async Task GetBinDaysTest(string postcode)
	{
		await TestSteps.EndToEnd(
			_client,
			_collectorService,
			_collector,
			postcode,
			_outputHelper,
			addressIndex: 1
		);
	}
}
