namespace BinDays.Api.IntegrationTests.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors;
	using BinDays.Api.Collectors.Collectors.Councils;
	using BinDays.Api.Collectors.Services;
	using BinDays.Api.IntegrationTests.Helpers;
	using System.Threading.Tasks;
	using Xunit;
	using Xunit.Abstractions;

	public class BuckinghamshireCouncilTests
	{
		private readonly IntegrationTestClient _client;
		private static readonly ICollector _collector = new BuckinghamshireCouncil();
		private readonly CollectorService _collectorService = new([_collector]);
		private readonly ITestOutputHelper _outputHelper;

		public BuckinghamshireCouncilTests(ITestOutputHelper outputHelper)
		{
			_outputHelper = outputHelper;
			_client = new IntegrationTestClient(outputHelper);
		}

		[Theory]
		[InlineData("HP22 5XA")] // Aylesbury Vale
		[InlineData("HP13 5AW")] // Wycombe
		[InlineData("HP9 1BG")]  // South Bucks
		[InlineData("HP7 0NQ")]  // Chiltern
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
