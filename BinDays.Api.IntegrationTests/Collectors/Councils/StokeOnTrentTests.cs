namespace BinDays.Api.IntegrationTests.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Councils;
using BinDays.Api.Collectors.Models;
using BinDays.Api.IntegrationTests.Helpers;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class StokeOnTrentTests
{
	private readonly IntegrationTestClient _client;
	private readonly ITestOutputHelper _outputHelper;
	private static readonly string _govUkId = new StokeOnTrent().GovUkId;

	public StokeOnTrentTests(ITestOutputHelper outputHelper)
	{
		_outputHelper = outputHelper;
		_client = new IntegrationTestClient(outputHelper);
	}

	[Theory]
	[InlineData("ST3 6HR", 0)]
	[InlineData("ST3 6HR", 29)]
	[InlineData("ST6 4BE", 0)]
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

	[Theory]
	[InlineData("ST6 4BE", 0)]
	public async Task GetBinDaysIncludesRecyclingForGreenBoxTest(string postcode, int addressIndex = 0)
	{
		var collectorResponse = await _client.ExecuteRequestCycleAsync<TestGetCollectorResponse>(
			$"/collector?postcode={postcode}",
			response => response.NextClientSideRequest
		);

		TestValidation.ValidateCollectorResult(collectorResponse.Collector, _govUkId);
		var collector = collectorResponse.Collector!;

		var addressesResponse = await _client.ExecuteRequestCycleAsync<GetAddressesResponse>(
			$"/{_govUkId}/addresses?postcode={postcode}",
			response => response.NextClientSideRequest
		);

		TestValidation.ValidateAddressesResult(addressesResponse.Addresses, ensureUidPresent: true);
		var selectedAddress = addressesResponse.Addresses!.ElementAt(addressIndex);

		var binDaysResponse = await _client.ExecuteRequestCycleAsync<GetBinDaysResponse>(
			$"/{_govUkId}/bin-days?postcode={postcode}&uid={selectedAddress.Uid!}&version={collector.Version}",
			response => response.NextClientSideRequest
		);

		TestValidation.ValidateBinDaysResult(
			binDaysResponse.BinDays,
			ensureBinsPresent: true,
			ensureFutureDates: true,
			ensureSortedByDate: true
		);

		var hasRecycling = binDaysResponse.BinDays!
			.SelectMany(binDay => binDay.Bins)
			.Any(bin => bin.Name == "Recycling");

		Assert.True(hasRecycling, "Expected at least one recycling collection for this postcode.");
	}
}
