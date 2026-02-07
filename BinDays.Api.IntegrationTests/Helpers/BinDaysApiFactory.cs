namespace BinDays.Api.IntegrationTests.Helpers;

using Microsoft.AspNetCore.Mvc.Testing;

/// <summary>
/// Provides a shared in-memory test server for integration tests.
/// The factory is lazily initialised on first use and reused across all test classes.
/// </summary>
internal static class BinDaysApiFactory
{
	private static readonly Lazy<WebApplicationFactory<Program>> _factory = new(
		() => new WebApplicationFactory<Program>()
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
