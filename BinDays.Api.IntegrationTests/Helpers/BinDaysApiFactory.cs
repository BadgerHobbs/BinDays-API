namespace BinDays.Api.IntegrationTests.Helpers;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Provides a shared in-memory test server for integration tests.
/// The factory is lazily initialised on first use and reused across all test classes.
/// </summary>
internal static class BinDaysApiFactory
{
	/// <summary>
	/// The API key configured for cache management endpoints during integration tests.
	/// </summary>
	public const string CacheApiKey = "integration-test-key";

	private static readonly Lazy<WebApplicationFactory<Program>> _factory = new(
		() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
		{
			builder.ConfigureAppConfiguration((_, config) =>
			{
				config.AddInMemoryCollection(new Dictionary<string, string?>
				{
					["CacheApiKey"] = CacheApiKey,
				});
			});
		})
	);

	/// <summary>
	/// Creates an <see cref="HttpClient"/> configured to call the in-memory API over HTTPS.
	/// </summary>
	/// <returns>An <see cref="HttpClient"/> with BaseAddress set to https://localhost.</returns>
	public static HttpClient CreateClient() => _factory.Value.CreateClient(
		new WebApplicationFactoryClientOptions
		{
			BaseAddress = new Uri("https://localhost"),
		}
	);
}
