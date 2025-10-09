namespace BinDays.Api.Collectors.Models
{
	using System.Collections.ObjectModel;

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
		required public BinColor Colour { get; init; }

		/// <summary>
		/// Gets bin type.
		/// </summary>
		public BinType? Type { get; init; }

		/// <summary>
		/// Gets bin keys (identifiers).
		/// </summary>
		required public ReadOnlyCollection<string> Keys { get; init; }
	}
}
