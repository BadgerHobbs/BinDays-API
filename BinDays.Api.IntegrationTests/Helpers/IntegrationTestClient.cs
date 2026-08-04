namespace BinDays.Api.IntegrationTests.Helpers;

using BinDays.Api.Collectors.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

/// <summary>
/// A client helper for executing multi-step requests during integration tests.
/// Posts to the real API endpoints and spawns the Dart/Dio CLI for external requests.
/// </summary>
internal sealed class IntegrationTestClient
{
	private readonly HttpClient _apiClient;
	private readonly string _dartCliPath;
	private readonly ITestOutputHelper _outputHelper;

	private static readonly JsonSerializerOptions _jsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};

	/// <summary>
	/// Initializes a new instance of the <see cref="IntegrationTestClient"/> class.
	/// </summary>
	/// <param name="outputHelper">The xUnit test output helper.</param>
	public IntegrationTestClient(ITestOutputHelper outputHelper)
	{
		_apiClient = BinDaysApiFactory.CreateClient();
		_outputHelper = outputHelper;
		_dartCliPath = ResolveDartCliPath();
	}

	/// <summary>
	/// Resolves the path to the compiled Dart CLI executable.
	/// Uses BINDAYS_DART_CLI_PATH env var if set, otherwise falls back to the default relative path.
	/// </summary>
	private static string ResolveDartCliPath()
	{
		var envPath = Environment.GetEnvironmentVariable("BINDAYS_DART_CLI_PATH");
		if (!string.IsNullOrEmpty(envPath))
		{
			return envPath;
		}

		// Default: relative to the test project directory
		var testProjectDir = Path.GetDirectoryName(typeof(IntegrationTestClient).Assembly.Location)!;
		return Path.GetFullPath(Path.Combine(testProjectDir, "..", "..", "..", "DartClient", "bin", "send_request.exe"));
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
	/// GETs an API endpoint that requires no client-side request cycle.
	/// </summary>
	/// <typeparam name="TResponse">The type of the API response.</typeparam>
	/// <param name="apiUrl">The API URL to GET.</param>
	/// <returns>The deserialized response.</returns>
	public async Task<TResponse> GetAsync<TResponse>(string apiUrl)
	{
		using var response = await _apiClient.GetAsync(apiUrl);
		response.EnsureSuccessStatusCode();
		var result = await response.Content.ReadFromJsonAsync<TResponse>();
		return result ?? throw new InvalidOperationException($"API returned null response from {apiUrl}.");
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
	/// Sends a single client-side HTTP request by spawning the Dart/Dio CLI process. The CLI
	/// executes the request using the real Dio HTTP client with the libcurl-impersonate transport —
	/// the same transport the app uses for every request — so the tests present a real browser's
	/// TLS/HTTP-2 fingerprint and share one code path with production.
	/// </summary>
	private async Task<ClientSideResponse> SendClientSideRequestAsync(ClientSideRequest request)
	{
		var requestJson = JsonSerializer.Serialize(request, _jsonOptions);

		using var process = new Process();
		process.StartInfo = new ProcessStartInfo
		{
			FileName = _dartCliPath,
			RedirectStandardInput = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		process.Start();

		await process.StandardInput.WriteAsync(requestJson);
		process.StandardInput.Close();

		var stdoutTask = process.StandardOutput.ReadToEndAsync();
		var stderrTask = process.StandardError.ReadToEndAsync();

		var exited = process.WaitForExit(30_000);
		if (!exited)
		{
			process.Kill();
			throw new TimeoutException("Dart CLI process timed out after 30 seconds.");
		}

		var stdout = await stdoutTask;
		var stderr = await stderrTask;

		_outputHelper.WriteLine($"[Dart CLI] Exit code: {process.ExitCode}");
		if (!string.IsNullOrEmpty(stderr))
		{
			_outputHelper.WriteLine($"[Dart CLI] stderr: {stderr}");
		}

		if (process.ExitCode != 0)
		{
			throw new HttpRequestException($"Dart CLI failed (exit code {process.ExitCode}): {stderr}");
		}

		return JsonSerializer.Deserialize<ClientSideResponse>(stdout, _jsonOptions)
			?? throw new InvalidOperationException("Dart CLI returned null response.");
	}
}
