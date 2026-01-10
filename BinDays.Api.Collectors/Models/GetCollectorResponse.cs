namespace BinDays.Api.Collectors.Models;

using BinDays.Api.Collectors.Collectors;
using Newtonsoft.Json;

/// <summary>
/// Represents the response from an collector lookup request.
/// This response can either contain the next client-side request to be made,
/// or the final collector found.
/// </summary>
public sealed class GetCollectorResponse
{
	/// <summary>
	/// Gets the next client-side request to be made, if further requests are required.
	/// </summary>
	public ClientSideRequest? NextClientSideRequest { get; init; }

	/// <summary>
	/// Gets the collector found, if no further client-side requests are required.
	/// </summary>
	[JsonProperty(TypeNameHandling = TypeNameHandling.Auto)]
	public ICollector? Collector { get; init; }
}
