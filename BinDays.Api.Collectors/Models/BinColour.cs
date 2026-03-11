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
	/// Red (#FF0000).
	/// </summary>
	public static readonly BinColour Red = new("Red", "#FF0000");

	/// <summary>
	/// Green (#008000).
	/// </summary>
	public static readonly BinColour Green = new("Green", "#008000");

	/// <summary>
	/// Light green (#90EE90).
	/// </summary>
	public static readonly BinColour LightGreen = new("Light Green", "#90EE90");

	/// <summary>
	/// Blue (#0000FF).
	/// </summary>
	public static readonly BinColour Blue = new("Blue", "#0000FF");

	/// <summary>
	/// Light blue (#ADD8E6).
	/// </summary>
	public static readonly BinColour LightBlue = new("Light Blue", "#ADD8E6");

	/// <summary>
	/// Black (#000000).
	/// </summary>
	public static readonly BinColour Black = new("Black", "#000000");

	/// <summary>
	/// Grey (#808080).
	/// </summary>
	public static readonly BinColour Grey = new("Grey", "#808080");

	/// <summary>
	/// Yellow (#FFFF00).
	/// </summary>
	public static readonly BinColour Yellow = new("Yellow", "#FFFF00");

	/// <summary>
	/// Orange (#FFA500).
	/// </summary>
	public static readonly BinColour Orange = new("Orange", "#FFA500");

	/// <summary>
	/// Purple (#800080).
	/// </summary>
	public static readonly BinColour Purple = new("Purple", "#800080");

	/// <summary>
	/// Pink (#FFC0CB).
	/// </summary>
	public static readonly BinColour Pink = new("Pink", "#FFC0CB");

	/// <summary>
	/// Brown (#A52A2A).
	/// </summary>
	public static readonly BinColour Brown = new("Brown", "#A52A2A");

	/// <summary>
	/// White (#FFFFFF).
	/// </summary>
	public static readonly BinColour White = new("White", "#FFFFFF");
}
