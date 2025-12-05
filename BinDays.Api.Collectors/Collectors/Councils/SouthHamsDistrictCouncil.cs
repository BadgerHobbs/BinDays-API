namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors.Vendors;
	using BinDays.Api.Collectors.Models;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;

	/// <summary>
	/// Collector implementation for South Hams District Council.
	/// </summary>
	internal sealed class SouthHamsDistrictCouncil : FccCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "South Hams District Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://waste.southhams.gov.uk/");

		/// <inheritdoc/>
		public override string GovUkId => "south-hams";

		/// <inheritdoc/>
		protected override string BaseUrl => "https://waste.southhams.gov.uk/";

		/// <inheritdoc/>
		protected override string CollectionDetailsEndpoint => "mycollections/getcollectiondetails";

		/// <inheritdoc/>
		protected override ReadOnlyCollection<Bin> BinTypes => new List<Bin>()
		{
			new()
			{
				Name = "Recycling",
				Colour = BinColour.Green,
				Keys = ["Recycling"],
			},
			new()
			{
				Name = "Refuse",
				Colour = BinColour.Grey,
				Keys = ["Refuse"],
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Brown,
				Keys = ["Garden"],
			},
		}.AsReadOnly();
	}
}
