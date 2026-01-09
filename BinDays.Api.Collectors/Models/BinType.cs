namespace BinDays.Api.Collectors.Models
{
	using System.Text.Json.Serialization;

	/// <summary>
	/// Represents the type of a bin.
	/// </summary>
	/// <remarks>
	/// For compatibility with the BinDays-App, this enum is serialized to a string.
	/// </remarks>
	[JsonConverter(typeof(JsonStringEnumConverter))]
	public enum BinType
	{
		/// <summary>
		/// A standard wheeled bin.
		/// </summary>
		Bin,

		/// <summary>
		/// A box for recycling.
		/// </summary>
		Box,

		/// <summary>
		/// A bag for waste or recycling.
		/// </summary>
		Bag,

		/// <summary>
		/// A small caddy, typically for food waste.
		/// </summary>
		Caddy,

		/// <summary>
		/// A sack for waste or recycling.
		/// </summary>
		Sack,

		/// <summary>
		/// A generic container for waste.
		/// </summary>
		Container,
	}
}

