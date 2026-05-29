namespace BinDays.Api.IntegrationTests.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Councils;
using BinDays.Api.IntegrationTests.Helpers;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class IpswichBoroughCouncilTests
{
	private readonly IntegrationTestClient _client;
	private readonly ITestOutputHelper _outputHelper;
	private static readonly string _govUkId = new IpswichBoroughCouncil().GovUkId;

	public IpswichBoroughCouncilTests(ITestOutputHelper outputHelper)
	{
		_outputHelper = outputHelper;
		_client = new IntegrationTestClient(outputHelper);
	}

	[Theory]
	[InlineData("IP3 0SP")]
	[InlineData("IP2 9PD")]
	public async Task GetBinDaysTest(string postcode)
	{
		await TestSteps.EndToEnd(
			_client,
			postcode,
			_govUkId,
			_outputHelper
		);
	}
}
