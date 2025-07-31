namespace BinDays.Api.Collectors.Models
{
	using System;
	using System.Collections.ObjectModel;

	/// <summary>
	/// Model which represents a bin day for a given collector.
	/// </summary>
	public sealed class BinDay
	{
		/// <summary>
		/// Backing field for the Bins property.
		/// Initialized with `null!` to satisfy the compiler's non-nullable check.
		/// The 'required' modifier on the public property ensures this is always
		/// assigned a valid collection during object initialization.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> _bins = null!;

		/// <summary>
		/// Gets bin day date.
		/// </summary>
		required public DateOnly Date { get; init; }

		/// <summary>
		/// Gets bin day address.
		/// </summary>
		required public Address Address { get; init; }

		/// <summary>
		/// Gets the bins to be collected on this day.
		/// This collection is guaranteed to not be null or empty.
		/// </summary>
		required public ReadOnlyCollection<Bin> Bins
		{
			get => _bins;
			init
			{
				// Validate that the incoming collection is not null or empty.
				if (value is null || value.Count == 0)
				{
					// Throw an exception if validation fails. This prevents an invalid
					// BinDay object from ever being created.
					throw new ArgumentException("Bins collection cannot be null or empty.", nameof(Bins));
				}
				_bins = value;
			}
		}
	}
}