namespace BinDays.Api.IntegrationTests.Collectors.Councils;

using BinDays.Api.Collectors.Collectors;
using BinDays.Api.Collectors.Collectors.Councils;
using BinDays.Api.Collectors.Services;
using BinDays.Api.IntegrationTests.Helpers;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class NorthTynesideCouncilTests
{
	private readonly IntegrationTestClient _client;
	private static readonly ICollector _collector = new NorthTynesideCouncil();
	private readonly CollectorService _collectorService = new([_collector]);
	private readonly ITestOutputHelper _outputHelper;

	public NorthTynesideCouncilTests(ITestOutputHelper outputHelper)
	{
		_outputHelper = outputHelper;
		_client = new IntegrationTestClient(outputHelper);
	}

	[Theory]
	[InlineData("NE28 7BH")]
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
