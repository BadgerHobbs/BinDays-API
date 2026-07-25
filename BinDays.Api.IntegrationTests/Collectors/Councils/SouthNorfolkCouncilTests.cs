namespace BinDays.Api.IntegrationTests.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Councils;
using BinDays.Api.Collectors.Models;
using BinDays.Api.IntegrationTests.Helpers;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class SouthNorfolkCouncilTests
{
	private readonly IntegrationTestClient _client;
	private readonly ITestOutputHelper _outputHelper;
	private static readonly string _govUkId = new SouthNorfolkCouncil().GovUkId;

	public SouthNorfolkCouncilTests(ITestOutputHelper outputHelper)
	{
		_outputHelper = outputHelper;
		_client = new IntegrationTestClient(outputHelper);
	}

	[Theory]
	[InlineData("NR18 0HQ")]
	[InlineData("NR8 5GS", 16)]
	public async Task GetBinDaysTest(string postcode, int addressIndex = 0)
	{
		await TestSteps.EndToEnd(
			_client,
			postcode,
			_govUkId,
			_outputHelper,
			addressIndex
		);
	}

	[Fact]
	public async Task GetBinDaysIncludesFoodWasteForIp214QuTest()
	{
		const string postcode = "IP21 4QU";
		const int addressIndex = 10;

		var collectorResponse = await _client.ExecuteRequestCycleAsync<TestGetCollectorResponse>(
			$"/collector?postcode={postcode}",
			response => response.NextClientSideRequest
		);

		var collector = collectorResponse.Collector!;

		var addressesResponse = await _client.ExecuteRequestCycleAsync<GetAddressesResponse>(
			$"/{_govUkId}/addresses?postcode={postcode}",
			response => response.NextClientSideRequest
		);

		var address = addressesResponse.Addresses!.ElementAt(addressIndex);

		var binDaysResponse = await _client.ExecuteRequestCycleAsync<GetBinDaysResponse>(
			$"/{_govUkId}/bin-days?postcode={postcode}&uid={address.Uid!}&version={collector.Version}",
			response => response.NextClientSideRequest
		);

		Assert.Contains(
			binDaysResponse.BinDays!.SelectMany(binDay => binDay.Bins),
			bin => bin.Name == "Food Waste"
		);
	}
}
