namespace BinDays.Api.IntegrationTests.Cache;

using BinDays.Api.IntegrationTests.Helpers;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

public sealed class CacheControllerTests
{
	private static readonly string _apiKey = BinDaysApiFactory.CacheApiKey;

	private readonly HttpClient _client = BinDaysApiFactory.CreateClient();

	[Fact]
	public async Task GetCache_WithoutApiKey_Returns401()
	{
		var response = await _client.GetAsync("/cache?postcode=SW1A0AA");

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}

	[Fact]
	public async Task GetCache_WithWrongApiKey_Returns401()
	{
		var request = new HttpRequestMessage(HttpMethod.Get, "/cache?postcode=SW1A0AA");
		request.Headers.Add("X-Api-Key", "wrong-key");

		var response = await _client.SendAsync(request);

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}

	[Fact]
	public async Task GetCache_WithNoFilters_Returns400()
	{
		var request = new HttpRequestMessage(HttpMethod.Get, "/cache");
		request.Headers.Add("X-Api-Key", _apiKey);

		var response = await _client.SendAsync(request);

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	[Fact]
	public async Task GetCache_WithInvalidType_Returns400()
	{
		var request = new HttpRequestMessage(HttpMethod.Get, "/cache?postcode=SW1A0AA&type=invalid");
		request.Headers.Add("X-Api-Key", _apiKey);

		var response = await _client.SendAsync(request);

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	[Fact]
	public async Task GetCache_WithPostcodeFilter_Returns200()
	{
		var request = new HttpRequestMessage(HttpMethod.Get, "/cache?postcode=SW1A0AA");
		request.Headers.Add("X-Api-Key", _apiKey);

		var response = await _client.SendAsync(request);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}

	[Fact]
	public async Task GetCache_WithGovUkIdFilter_Returns200()
	{
		var request = new HttpRequestMessage(HttpMethod.Get, "/cache?govUkId=E09000001");
		request.Headers.Add("X-Api-Key", _apiKey);

		var response = await _client.SendAsync(request);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}

	[Fact]
	public async Task GetCache_WithUidFilter_Returns200()
	{
		var request = new HttpRequestMessage(HttpMethod.Get, "/cache?uid=test-uid");
		request.Headers.Add("X-Api-Key", _apiKey);

		var response = await _client.SendAsync(request);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}

	[Fact]
	public async Task GetCache_WithTypeFilter_Returns200()
	{
		var request = new HttpRequestMessage(HttpMethod.Get, "/cache?postcode=SW1A0AA&type=collectors,addresses,collections");
		request.Headers.Add("X-Api-Key", _apiKey);

		var response = await _client.SendAsync(request);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}

	[Fact]
	public async Task DeleteCache_WithoutApiKey_Returns401()
	{
		var request = new HttpRequestMessage(HttpMethod.Delete, "/cache?postcode=SW1A0AA");

		var response = await _client.SendAsync(request);

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}

	[Fact]
	public async Task DeleteCache_WithNoFilters_Returns400()
	{
		var request = new HttpRequestMessage(HttpMethod.Delete, "/cache");
		request.Headers.Add("X-Api-Key", _apiKey);

		var response = await _client.SendAsync(request);

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	[Fact]
	public async Task DeleteCache_WithPostcodeFilter_Returns200WithCount()
	{
		var request = new HttpRequestMessage(HttpMethod.Delete, "/cache?postcode=SW1A0AA");
		request.Headers.Add("X-Api-Key", _apiKey);

		var response = await _client.SendAsync(request);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		Assert.True(body.TryGetProperty("keysRemoved", out var keysRemoved));
		Assert.Equal(JsonValueKind.Number, keysRemoved.ValueKind);
	}
}
