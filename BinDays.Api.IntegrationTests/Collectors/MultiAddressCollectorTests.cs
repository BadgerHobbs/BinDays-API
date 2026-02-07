namespace BinDays.Api.IntegrationTests.Collectors;

using BinDays.Api.IntegrationTests.Helpers;
using System.Net;
using Xunit;
using Xunit.Abstractions;

public class MultiAddressCollectorTests
{
	private readonly IntegrationTestClient _client;
	private const string _postcode = "SS9 3RE";

	public MultiAddressCollectorTests(ITestOutputHelper outputHelper)
	{
		_client = new IntegrationTestClient(outputHelper);
	}

	[Fact]
	public async Task GetCollectorReturnsNotFound()
	{
		using var response = await _client.ExecuteRequestCycleRawAsync(
			$"/collector?postcode={Uri.EscapeDataString(_postcode)}"
		);

		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
	}
}
