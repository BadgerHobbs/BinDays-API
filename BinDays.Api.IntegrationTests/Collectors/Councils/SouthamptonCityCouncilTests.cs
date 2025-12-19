namespace BinDays.Api.IntegrationTests.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors;
	using BinDays.Api.Collectors.Collectors.Councils;
	using BinDays.Api.Collectors.Services;
	using BinDays.Api.IntegrationTests.Helpers;
	using System.Threading.Tasks;
	using Xunit;
	using Xunit.Abstractions;

	public class SouthamptonCityCouncilTests
	{
		private readonly IntegrationTestClient _client;
		private static readonly ICollector _collector = new SouthamptonCityCouncil();
		private readonly CollectorService _collectorService = new([_collector]);
		private readonly ITestOutputHelper _outputHelper;

		public SouthamptonCityCouncilTests(ITestOutputHelper outputHelper)
		{
			_outputHelper = outputHelper;
			_client = new IntegrationTestClient(outputHelper);
		}

		[Theory]
		[InlineData("SO15 5NR")]
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
}
