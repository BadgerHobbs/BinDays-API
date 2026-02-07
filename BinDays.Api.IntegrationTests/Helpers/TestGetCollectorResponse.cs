namespace BinDays.Api.IntegrationTests.Helpers;

using BinDays.Api.Collectors.Models;

/// <summary>
/// Test-only mirror of <see cref="GetCollectorResponse"/> that uses <see cref="TestCollector"/>
/// instead of <c>ICollector</c>, allowing System.Text.Json deserialization.
/// </summary>
internal sealed class TestGetCollectorResponse
{
	/// <summary>
	/// Gets or sets the next client-side request to be made, if further requests are required.
	/// </summary>
	public ClientSideRequest? NextClientSideRequest { get; set; }

	/// <summary>
	/// Gets or sets the collector found, if no further client-side requests are required.
	/// </summary>
	public TestCollector? Collector { get; set; }
}
