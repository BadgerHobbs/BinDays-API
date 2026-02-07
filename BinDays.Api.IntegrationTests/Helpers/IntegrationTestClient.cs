namespace BinDays.Api.IntegrationTests.Helpers;

using BinDays.Api.Collectors.Models;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using Xunit.Abstractions;

/// <summary>
/// A client helper for executing multi-step requests during integration tests.
/// Posts to the real API endpoints and executes external client-side requests.
/// </summary>
internal sealed class IntegrationTestClient
{
	private readonly HttpClient _apiClient;
	private readonly HttpClient _httpClientWithRedirects;
	private readonly HttpClient _httpClientWithoutRedirects;

	/// <summary>
	/// Initializes a new instance of the <see cref="IntegrationTestClient"/> class.
	/// </summary>
	/// <param name="outputHelper">The xUnit test output helper.</param>
	public IntegrationTestClient(ITestOutputHelper outputHelper)
	{
		_apiClient = BinDaysApiFactory.CreateClient();

		var enableHttpLogging = string.Equals(
			Environment.GetEnvironmentVariable("BINDAYS_ENABLE_HTTP_LOGGING"),
			"true",
			StringComparison.OrdinalIgnoreCase
		);

		// Client that automatically follows redirects
		var handlerWithRedirects = new HttpClientHandler
		{
			UseCookies = false,
			CookieContainer = new CookieContainer(),
			AllowAutoRedirect = true,
		};
		_httpClientWithRedirects = CreateExternalClient(handlerWithRedirects, enableHttpLogging, outputHelper);

		// Client that does NOT automatically follow redirects
		var handlerWithoutRedirects = new HttpClientHandler
		{
			UseCookies = false,
			CookieContainer = new CookieContainer(),
			AllowAutoRedirect = false,
		};
		_httpClientWithoutRedirects = CreateExternalClient(handlerWithoutRedirects, enableHttpLogging, outputHelper);
	}

	/// <summary>
	/// Creates an <see cref="HttpClient"/> for external (council website) requests.
	/// </summary>
	private static HttpClient CreateExternalClient(HttpClientHandler handler, bool enableLogging, ITestOutputHelper outputHelper)
	{
		return enableLogging
			? new HttpClient(new LoggingHttpHandler(outputHelper, handler))
			: new HttpClient(handler);
	}

	/// <summary>
	/// Executes the full request cycle by POSTing to an API endpoint and following
	/// client-side request chains until the final response is returned.
	/// </summary>
	/// <typeparam name="TResponse">The type of the API response.</typeparam>
	/// <param name="apiUrl">The API URL to POST to.</param>
	/// <param name="nextRequestExtractor">Extracts the NextClientSideRequest from the response.</param>
	/// <returns>The final API response.</returns>
	public async Task<TResponse> ExecuteRequestCycleAsync<TResponse>(
		string apiUrl,
		Func<TResponse, ClientSideRequest?> nextRequestExtractor)
	{
		ClientSideResponse? clientSideResponse = null;

		while (true)
		{
			var apiResponse = await PostToApiAsync<TResponse>(apiUrl, clientSideResponse);

			var nextRequest = nextRequestExtractor(apiResponse);
			if (nextRequest == null)
			{
				return apiResponse;
			}

			clientSideResponse = await SendClientSideRequestAsync(nextRequest);
		}
	}

	/// <summary>
	/// Executes the full request cycle, following client-side requests, and returns
	/// the final raw HttpResponseMessage. Used for asserting HTTP status codes.
	/// </summary>
	/// <param name="apiUrl">The API URL to POST to.</param>
	/// <returns>The final raw HttpResponseMessage.</returns>
	public async Task<HttpResponseMessage> ExecuteRequestCycleRawAsync(string apiUrl)
	{
		ClientSideResponse? clientSideResponse = null;

		while (true)
		{
			var response = await _apiClient.PostAsJsonAsync(apiUrl, clientSideResponse);

			if (!response.IsSuccessStatusCode)
			{
				return response;
			}

			var body = await response.Content.ReadFromJsonAsync<TestGetCollectorResponse>();

			if (body?.NextClientSideRequest == null)
			{
				return response;
			}

			response.Dispose();
			clientSideResponse = await SendClientSideRequestAsync(body.NextClientSideRequest);
		}
	}

	/// <summary>
	/// Posts to the API endpoint with an optional ClientSideResponse body.
	/// </summary>
	private async Task<TResponse> PostToApiAsync<TResponse>(string apiUrl, ClientSideResponse? clientSideResponse)
	{
		using var response = await _apiClient.PostAsJsonAsync(apiUrl, clientSideResponse);
		response.EnsureSuccessStatusCode();
		var result = await response.Content.ReadFromJsonAsync<TResponse>();
		return result ?? throw new InvalidOperationException($"API returned null response from {apiUrl}.");
	}

	/// <summary>
	/// Sends a single client-side HTTP request as defined by the API.
	/// </summary>
	private async Task<ClientSideResponse> SendClientSideRequestAsync(ClientSideRequest request)
	{
		using var httpRequest = new HttpRequestMessage(new HttpMethod(request.Method), request.Url);
		var headersToSend = new Dictionary<string, string>(request.Headers, StringComparer.OrdinalIgnoreCase);

		if (!string.IsNullOrEmpty(request.Body))
		{
			var mediaTypeOnly = "application/octet-stream";
			var requestEncoding = Encoding.UTF8;

			var contentTypeKey = headersToSend.Keys.FirstOrDefault(k => k.Equals("content-type", StringComparison.OrdinalIgnoreCase));

			if (contentTypeKey != null)
			{
				var fullContentType = headersToSend[contentTypeKey];
				headersToSend.Remove(contentTypeKey);

				var parts = fullContentType.Split(';');
				mediaTypeOnly = parts[0].Trim();
			}
			else if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) && LooksLikeJson(request.Body))
			{
				mediaTypeOnly = "application/json";
			}

			httpRequest.Content = new StringContent(request.Body, requestEncoding, mediaTypeOnly);
		}

		foreach (var header in headersToSend)
		{
			if (!httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value))
			{
				httpRequest.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
			}
		}

		// Choose the appropriate HttpClient based on the FollowRedirects option
		var httpClient = request.Options.FollowRedirects ? _httpClientWithRedirects : _httpClientWithoutRedirects;

		using var httpResponse = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseContentRead);
		var responseContent = await httpResponse.Content.ReadAsStringAsync();

		var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var header in httpResponse.Headers.Concat(httpResponse.Content.Headers))
		{
			// Convert headers to lowercase to match BinDays-Client library
			responseHeaders[header.Key.ToLower()] = string.Join(",", header.Value);
		}

		return new ClientSideResponse
		{
			RequestId = request.RequestId,
			StatusCode = (int)httpResponse.StatusCode,
			Headers = responseHeaders,
			Content = responseContent,
			ReasonPhrase = httpResponse.ReasonPhrase ?? string.Empty,
			Options = request.Options,
		};
	}

	/// <summary>
	/// Basic check to see if a string looks like a JSON object or array.
	/// </summary>
	private static bool LooksLikeJson(string value)
	{
		if (string.IsNullOrWhiteSpace(value)) return false;
		var trimmedValue = value.Trim();

		return (trimmedValue.StartsWith('{') && trimmedValue.EndsWith('}')) ||
			   (trimmedValue.StartsWith('[') && trimmedValue.EndsWith(']'));
	}
}
