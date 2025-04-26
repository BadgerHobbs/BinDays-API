namespace BinDays.Api.IntegrationTests.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors;
	using BinDays.Api.Collectors.Collectors.Councils;
	using BinDays.Api.Collectors.Services;
	using BinDays.Api.IntegrationTests.Helpers;
	using System.Linq;
	using System.Threading.Tasks;
	using Xunit;
	using Xunit.Abstractions;

	public class SouthGloucestershireCouncilTests
	{
		private readonly IntegrationTestClient _client = new();
		private static readonly ICollector _collector = new SouthGloucestershireCouncil();
		private readonly CollectorService _collectorService = new([_collector]);
		private const string _postcode = "BS167ES";

		private readonly ITestOutputHelper _outputHelper;

		public SouthGloucestershireCouncilTests(ITestOutputHelper outputHelper)
		{
			_outputHelper = outputHelper;
		}

		[Fact]
		public async Task GetBinDaysTest()
		{
			// Step 1: Get Collector
			var collector = await TestSteps.GetCollectorAsync(
				_client,
				_collectorService,
				_postcode,
				_collector.GetType(),
				_collector.GovUkId
			);

			// Step 2: Get Addresses
			var addresses = await TestSteps.GetAddressesAsync(
				_client,
				collector,
				_postcode
			);

			var selectedAddress = addresses.First();

			// Step 3: Get Bin Days
			var binDays = await TestSteps.GetBinDaysAsync(
				_client,
				collector,
				selectedAddress
			);

			// Step 4: Output Summary
			TestOutput.WriteTestSummary(
				_outputHelper,
				collector,
				addresses,
				binDays
			);
		}
	}
}
