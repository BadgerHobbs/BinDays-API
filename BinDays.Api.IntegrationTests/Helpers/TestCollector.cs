namespace BinDays.Api.IntegrationTests.Helpers;

/// <summary>
/// Test-only concrete class for deserializing the collector from API responses.
/// System.Text.Json cannot deserialize the <c>ICollector</c> interface directly.
/// </summary>
internal sealed class TestCollector
{
	/// <summary>
	/// Gets or sets the display name of the collector (e.g. "Birmingham City Council").
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the GOV.UK identifier for the collector (e.g. "birmingham").
	/// </summary>
	public string GovUkId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the council website URL.
	/// </summary>
	public string? WebsiteUrl { get; set; }

	/// <summary>
	/// Gets or sets the GOV.UK page URL for the collector.
	/// </summary>
	public string? GovUkUrl { get; set; }
}
