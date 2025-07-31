namespace BinDays.Api.Collectors.Models
{
	using System.Collections.ObjectModel;

	/// <summary>
	/// Model which represents a bin day for a given collector.
	/// </summary>
	public sealed class BinDay
	{
		/// <summary>
		/// Gets bin day date.
		/// </summary>
		required public DateOnly Date { get; init; }

		/// <summary>
		/// Gets bin day address.
		/// </summary>
		required public Address Address { get; init; }

		/// <summary>
		/// Gets bin day bins.
		/// </summary>
		required public ReadOnlyCollection<Bin> Bins { get; init; }
	}
}
