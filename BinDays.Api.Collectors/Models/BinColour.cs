namespace BinDays.Api.Collectors.Models
{
	using BinDays.Api.Converters;
	using System.Text.Json.Serialization;

	/// <summary>
	/// Represents the colour of a bin.
	/// </summary>
	/// <remarks>
	/// For compatibility with the BinDays-App, this enum is serialized to a
	/// space-separated string (e.g. LightBlue -> "Light Blue").
	/// </remarks>
	[JsonConverter(typeof(SpacedPascalCaseEnumConverter<BinColour>))]
	public enum BinColour
	{
		/// <summary>
		/// Red bin.
		/// </summary>
		Red,

		/// <summary>
		/// Green bin.
		/// </summary>
		Green,

		/// <summary>
		/// Light green bin.
		/// </summary>
		LightGreen,

		/// <summary>
		/// Blue bin.
		/// </summary>
		Blue,

		/// <summary>
		/// Light blue bin.
		/// </summary>
		LightBlue,

		/// <summary>
		/// Black bin.
		/// </summary>
		Black,

		/// <summary>
		/// Grey bin.
		/// </summary>
		Grey,

		/// <summary>
		/// Yellow bin.
		/// </summary>
		Yellow,

		/// <summary>
		/// Orange bin.
		/// </summary>
		Orange,

		/// <summary>
		/// Purple bin.
		/// </summary>
		Purple,

		/// <summary>
		/// Pink bin.
		/// </summary>
		Pink,

		/// <summary>
		/// Brown bin.
		/// </summary>
		Brown,

		/// <summary>
		/// White bin.
		/// </summary>
		White,
	}
}

