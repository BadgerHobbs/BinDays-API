namespace BinDays.Api.IntegrationTests.Helpers
{
	using BinDays.Api.Collectors.Models;
	using System.Linq;
	using System.Net;
	using System.Net.Http;
	using System.Net.Security;
	using System.Runtime.InteropServices;
	using System.Text;

	/// <summary>
	/// A client helper for executing multi-step requests during integration tests.
	/// </summary>
	internal sealed class IntegrationTestClient
	{
		private readonly HttpClient _httpClientWithRedirects;
		private readonly HttpClient _httpClientWithoutRedirects;

		/// <summary>
		/// Initializes a new instance of the <see cref="IntegrationTestClient"/> class.
		/// </summary>
		public IntegrationTestClient()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				var cipherSuitesPolicy = new CipherSuitesPolicy(
					new[]
					{
						TlsCipherSuite.TLS_AES_256_GCM_SHA384,
						TlsCipherSuite.TLS_CHACHA20_POLY1305_SHA256,
						TlsCipherSuite.TLS_AES_128_GCM_SHA256,
						TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384,
						TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384,
						TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_CHACHA20_POLY1305_SHA256,
						TlsCipherSuite.TLS_ECDHE_RSA_WITH_CHACHA20_POLY1305_SHA256,
						TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256,
						TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256
					});

				// Client that automatically follows redirects
				var handlerWithRedirects = new SocketsHttpHandler
				{
					UseCookies = false,
					CookieContainer = new CookieContainer(),
					AllowAutoRedirect = true,
					SslOptions = new SslClientAuthenticationOptions
					{
						CipherSuitesPolicy = cipherSuitesPolicy
					}
				};
				_httpClientWithRedirects = new HttpClient(handlerWithRedirects);

				// Client that does NOT automatically follow redirects
				var handlerWithoutRedirects = new SocketsHttpHandler
				{
					UseCookies = false,
					CookieContainer = new CookieContainer(),
					AllowAutoRedirect = false,
					SslOptions = new SslClientAuthenticationOptions
					{
						CipherSuitesPolicy = cipherSuitesPolicy
					}
				};
				_httpClientWithoutRedirects = new HttpClient(handlerWithoutRedirects);
			}
			else
			{
				// Client that automatically follows redirects
				var handlerWithRedirects = new HttpClientHandler
				{
					UseCookies = false,
					CookieContainer = new CookieContainer(),
					AllowAutoRedirect = true,
				};
				_httpClientWithRedirects = new HttpClient(handlerWithRedirects);

				// Client that does NOT automatically follow redirects
				var handlerWithoutRedirects = new HttpClientHandler
				{
					UseCookies = false,
					CookieContainer = new CookieContainer(),
					AllowAutoRedirect = false,
				};
				_httpClientWithoutRedirects = new HttpClient(handlerWithoutRedirects);
			}
		}

		/// <summary>
		/// Executes the entire request cycle for a multi-step API call.
		/// </summary>
		/// <typeparam name="TResponse">The type of the final API response object (e.g. GetAddressesResponse).</typeparam>
		/// <typeparam name="TResult">The type of the final data expected (e.g. IReadOnlyCollection<Address>).</typeparam>
		/// <param name="initialFunc">A function that makes the first call to the API method (with clientSideResponse = null).</param>
		/// <param name="subsequentFunc">A function that makes subsequent calls to the API method, passing the ClientSideResponse.</param>
		/// <param name="nextRequestExtractor">A function to extract the NextClientSideRequest from the API response.</param>
		/// <param name="resultExtractor">A function to extract the final TResult data from the API response.</param>
		/// <param name="errorMessage">Error message to throw if the cycle completes without yielding data.</param>
		/// <returns>The final extracted data of type TResult.</returns>
		/// <exception cref="InvalidOperationException">Thrown if the API does not return data or a next step.</exception>
		/// <exception cref="HttpRequestException">Can be thrown if a client-side request fails.</exception>
		public async Task<TResult> ExecuteRequestCycleAsync<TResponse, TResult>(
			Func<TResponse> initialFunc,
			Func<ClientSideResponse, TResponse> subsequentFunc,
			Func<TResponse, ClientSideRequest?> nextRequestExtractor,
			Func<TResponse, TResult?> resultExtractor,
			string errorMessage)
			where TResult : class
		{
			ClientSideResponse? clientSideResponse = null;
			TResponse apiResponse;

			while (true)
			{
				apiResponse = clientSideResponse == null
					? initialFunc()
					: subsequentFunc(clientSideResponse);

				TResult? result = resultExtractor(apiResponse);
				if (result != null)
				{
					return result;
				}

				ClientSideRequest? nextRequest = nextRequestExtractor(apiResponse);
				if (nextRequest != null)
				{
					clientSideResponse = await SendClientSideRequestAsync(nextRequest);

				}
				else
				{
					throw new InvalidOperationException(errorMessage);
				}
			}
		}

		/// <summary>
		/// Sends a single client-side HTTP request as defined by the API.
		/// </summary>
		/// <param name="request">The client-side request details.</param>
		/// <returns>The response from the external service, packaged as a ClientSideResponse.</returns>
		/// <exception cref="HttpRequestException">Thrown if the HTTP request fails.</exception>
		private async Task<ClientSideResponse> SendClientSideRequestAsync(ClientSideRequest request)
		{
			using var httpRequest = new HttpRequestMessage(new HttpMethod(request.Method), request.Url);
			var headersToSend = new Dictionary<string, string>(request.Headers, StringComparer.OrdinalIgnoreCase);

			if (!string.IsNullOrEmpty(request.Body))
			{
				string mediaTypeOnly = "application/octet-stream";
				Encoding requestEncoding = Encoding.UTF8;

				var contentTypeKey = headersToSend.Keys.FirstOrDefault(k => k.Equals("content-type", StringComparison.OrdinalIgnoreCase));

				if (contentTypeKey != null)
				{
					string fullContentType = headersToSend[contentTypeKey];
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
		/// <param name="value">The string to check.</param>
		/// <returns>True if it starts/ends with {} or [], false otherwise.</returns>
		private static bool LooksLikeJson(string value)
		{
			if (string.IsNullOrWhiteSpace(value)) return false;
			var trimmedValue = value.Trim();

			return (trimmedValue.StartsWith('{') && trimmedValue.EndsWith('}')) ||
				   (trimmedValue.StartsWith('[') && trimmedValue.EndsWith(']'));
		}
	}
}