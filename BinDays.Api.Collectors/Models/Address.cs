namespace BinDays.Api.Collectors.Models
{
	/// <summary>
	/// Model which represents an address for a given collector.
	/// </summary>
	public sealed class Address
	{
		/// <summary>
		/// Gets address property.
		/// </summary>
		public string? Property { get; init; }

		/// <summary>
		/// Gets address street.
		/// </summary>
		public string? Street { get; init; }

		/// <summary>
		/// Gets address town.
		/// </summary>
		public string? Town { get; init; }

		/// <summary>
		/// Gets address postcode.
		/// </summary>
		public string? Postcode { get; init; }

		/// <summary>
		/// Gets address uid.
		/// </summary>
		public string? Uid { get; init; }
	}
}
