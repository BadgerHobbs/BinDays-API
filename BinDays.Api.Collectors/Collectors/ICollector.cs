namespace BinDays.Api.Collectors.Collectors;

using BinDays.Api.Collectors.Models;

/// <summary>
/// Interface for a collector.
/// </summary>
public interface ICollector
{
	/// <summary>
	/// Gets the name of the collector.
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// Gets the website url of the collector.
	/// </summary>
	public Uri WebsiteUrl { get; }

	/// <summary>
	/// Gets the gov.uk id of the collector.
	/// </summary>
	public string GovUkId { get; }

	/// <summary>
	/// Gets the gov.uk url of the collector.
	/// </summary>
	public Uri GovUkUrl { get; }

	/// <summary>
	/// Gets the addresses for a given postcode, potentially requiring multiple steps via client-side responses.
	/// </summary>
	/// <param name="postcode">The postcode to search for.</param>
	/// <param name="clientSideResponse">The response from a previous client-side request, if applicable.</param>
	/// <returns>The response containing either the next client-side request to make or the addresses.</returns>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse);

	/// <summary>
	/// Gets the bin collection days for a given address, potentially requiring multiple steps via client-side responses.
	/// </summary>
	/// <param name="address">The address to get bin days for.</param>
	/// <param name="clientSideResponse">The response from a previous client-side request, if applicable.</param>
	/// <returns>The response containing either the next client-side request to make or the bin days.</returns>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse);
}
