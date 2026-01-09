namespace BinDays.Api.Collectors.Models;

/// <summary>
/// Represents the response from a bin day lookup request.
/// This response can either contain the next client-side request to be made,
/// or the final list of bin days found.
/// </summary>
public sealed class GetBinDaysResponse
{
	/// <summary>
	/// Gets the next client-side request to be made, if further requests are required.
	/// </summary>
	public ClientSideRequest? NextClientSideRequest { get; init; }

	/// <summary>
	/// Gets the list of bin days found, if no further client-side requests are required.
	/// </summary>
	public IReadOnlyCollection<BinDay>? BinDays { get; init; }
}
