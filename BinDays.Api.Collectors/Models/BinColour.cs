namespace BinDays.Api.Collectors.Models;

using BinDays.Api.Collectors.Converters;
using System.Text.Json.Serialization;

/// <summary>
/// Represents a bin colour with a display name and hex value.
/// </summary>
/// <remarks>
/// Serialized as the <see cref="Name"/> string for backwards compatibility.
/// The hex value is exposed via <see cref="Bin.ColourHex"/> in the API response.
/// </remarks>
[JsonConverter(typeof(BinColourJsonConverter))]
public sealed class BinColour
{
	/// <summary>
	/// Gets the display name (e.g. "Crimson", "Dark Cyan").
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// Gets the hex colour value (e.g. "#DC143C").
	/// </summary>
	public string Hex { get; }

	/// <summary>
	/// Creates a <see cref="BinColour"/>.
	/// </summary>
	public BinColour(string name, string hex)
	{
		Name = name;
		Hex = hex;
	}

	/// <summary>
	/// Red (#F44336). Matches Flutter <c>Colors.red</c>.
	/// </summary>
	public static readonly BinColour Red = new("Red", "#F44336");

	/// <summary>
	/// Green (#4CAF50). Matches Flutter <c>Colors.green</c>.
	/// </summary>
	public static readonly BinColour Green = new("Green", "#4CAF50");

	/// <summary>
	/// Light green (#8BC34A). Matches Flutter <c>Colors.lightGreen</c>.
	/// </summary>
	public static readonly BinColour LightGreen = new("Light Green", "#8BC34A");

	/// <summary>
	/// Blue (#2196F3). Matches Flutter <c>Colors.blue</c>.
	/// </summary>
	public static readonly BinColour Blue = new("Blue", "#2196F3");

	/// <summary>
	/// Light blue (#03A9F4). Matches Flutter <c>Colors.lightBlue</c>.
	/// </summary>
	public static readonly BinColour LightBlue = new("Light Blue", "#03A9F4");

	/// <summary>
	/// Black (#000000). Matches Flutter <c>Colors.black</c>.
	/// </summary>
	public static readonly BinColour Black = new("Black", "#000000");

	/// <summary>
	/// Grey (#9E9E9E). Matches Flutter <c>Colors.grey</c>.
	/// </summary>
	public static readonly BinColour Grey = new("Grey", "#9E9E9E");

	/// <summary>
	/// Clear (#9E9E9E). Same swatch as <see cref="Grey"/>, for items explicitly specified
	/// as going out in a clear/transparent bag (e.g. batteries), rather than an unspecified colour.
	/// </summary>
	public static readonly BinColour Clear = new("Clear", "#9E9E9E");

	/// <summary>
	/// Any (#9E9E9E). Same swatch as <see cref="Grey"/>, for items with no official council
	/// colour or receptacle (e.g. any bag left out alongside another bin). Combines with
	/// <see cref="Bin.Type"/> in the app as "Any Bag"/"Any Sack".
	/// </summary>
	public static readonly BinColour Any = new("Any", "#9E9E9E");

	/// <summary>
	/// Yellow (#FFEB3B). Matches Flutter <c>Colors.yellow</c>.
	/// </summary>
	public static readonly BinColour Yellow = new("Yellow", "#FFEB3B");

	/// <summary>
	/// Orange (#FF9800). Matches Flutter <c>Colors.orange</c>.
	/// </summary>
	public static readonly BinColour Orange = new("Orange", "#FF9800");

	/// <summary>
	/// Purple (#9C27B0). Matches Flutter <c>Colors.purple</c>.
	/// </summary>
	public static readonly BinColour Purple = new("Purple", "#9C27B0");

	/// <summary>
	/// Pink (#E91E63). Matches Flutter <c>Colors.pink</c>.
	/// </summary>
	public static readonly BinColour Pink = new("Pink", "#E91E63");

	/// <summary>
	/// Brown (#795548). Matches Flutter <c>Colors.brown</c>.
	/// </summary>
	public static readonly BinColour Brown = new("Brown", "#795548");

	/// <summary>
	/// White (#FFFFFF). Matches Flutter <c>Colors.white</c>.
	/// </summary>
	public static readonly BinColour White = new("White", "#FFFFFF");
}
