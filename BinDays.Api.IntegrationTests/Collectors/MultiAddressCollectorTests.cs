namespace BinDays.Api.IntegrationTests.Collectors;

using BinDays.Api.IntegrationTests.Helpers;
using System.Net;
using Xunit;
using Xunit.Abstractions;

public sealed class MultiAddressCollectorTests
{
	private readonly IntegrationTestClient _client;

	public MultiAddressCollectorTests(ITestOutputHelper outputHelper)
	{
		_client = new IntegrationTestClient(outputHelper);
	}

	[Fact]
	public async Task GetCollectorReturnsNotFound()
	{
		using var response = await _client.ExecuteRequestCycleRawAsync(
			"/collector?postcode=SS9 3RE"
		);

		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
	}
}
