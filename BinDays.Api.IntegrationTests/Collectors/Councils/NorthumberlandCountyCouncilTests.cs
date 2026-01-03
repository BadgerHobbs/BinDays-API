namespace BinDays.Api.IntegrationTests.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors;
	using BinDays.Api.Collectors.Collectors.Councils;
	using BinDays.Api.Collectors.Services;
	using BinDays.Api.IntegrationTests.Helpers;
	using System.Threading.Tasks;
	using Xunit;
	using Xunit.Abstractions;

	public class NorthumberlandCountyCouncilTests
	{
		private readonly IntegrationTestClient _client;
		private static readonly ICollector _collector = new NorthumberlandCountyCouncil();
		private readonly CollectorService _collectorService = new([_collector]);
		private readonly ITestOutputHelper _outputHelper;

		public NorthumberlandCountyCouncilTests(ITestOutputHelper outputHelper)
		{
			_outputHelper = outputHelper;
			_client = new IntegrationTestClient(outputHelper);
		}

		[Theory]
		[InlineData("NE46 3JR")]
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
