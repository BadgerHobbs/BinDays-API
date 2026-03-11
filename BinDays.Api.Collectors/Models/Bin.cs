namespace BinDays.Api.Collectors.Models;

/// <summary>
/// Model which represents an bin for a given collector.
/// </summary>
public sealed class Bin
{
	/// <summary>
	/// Gets bin name.
	/// </summary>
	required public string Name { get; init; }

	/// <summary>
	/// Gets bin colour.
	/// </summary>
	required public BinColour Colour { get; init; }

	/// <summary>
	/// Gets the hex value of the bin colour (e.g. "#FF0000").
	/// </summary>
	public string ColourHex => Colour.Hex;

	/// <summary>
	/// Gets bin type.
	/// </summary>
	public BinType? Type { get; init; }

	/// <summary>
	/// Gets bin keys (identifiers).
	/// </summary>
	required public IReadOnlyCollection<string> Keys { get; init; }
}
