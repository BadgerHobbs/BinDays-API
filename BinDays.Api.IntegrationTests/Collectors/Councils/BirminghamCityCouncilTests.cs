namespace BinDays.Api.IntegrationTests.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors;
	using BinDays.Api.Collectors.Collectors.Councils;
	using BinDays.Api.Collectors.Services;
	using BinDays.Api.IntegrationTests.Helpers;
	using System.Threading.Tasks;
	using Xunit;
	using Xunit.Abstractions;

	public class BirminghamCityCouncilTests
	{
		private readonly IntegrationTestClient _client = new();
		private static readonly ICollector _collector = new BirminghamCityCouncil();
		private readonly CollectorService _collectorService = new([_collector]);
		private const string _postcode = "B34 6BS";

		private readonly ITestOutputHelper _outputHelper;

		public BirminghamCityCouncilTests(ITestOutputHelper outputHelper)
		{
			_outputHelper = outputHelper;
		}

		[Fact]
		public async Task GetBinDaysTest()
		{
			await TestSteps.EndToEnd(
				_client,
				_collectorService,
				_collector,
				_postcode,
				_outputHelper
			);
		}
	}
}
