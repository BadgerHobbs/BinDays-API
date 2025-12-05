namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors.Vendors;
	using BinDays.Api.Collectors.Models;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;

	/// <summary>
	/// Collector implementation for Somerset Council.
	/// </summary>
	internal sealed class SomersetCouncil : ITouchVisionCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Somerset Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.somerset.gov.uk/bins-recycling-and-waste/check-my-collection-days/");

		/// <inheritdoc/>
		public override string GovUkId => "somerset";

		/// <inheritdoc/>
		protected override int ClientId => 129;

		/// <inheritdoc/>
		protected override int CouncilId => 34493;

		/// <inheritdoc/>
		protected override string ApiBaseUrl => "https://iweb.itouchvision.com/portal/itouchvision/";

		/// <inheritdoc/>
		protected override ReadOnlyCollection<Bin> BinTypes => new List<Bin>()
		{
			new()
			{
				Name = "Rubbish",
				Colour = BinColour.Black,
				Keys = ["Rubbish"],
			},
			new()
			{
				Name = "Recycling",
				Colour = BinColour.Blue,
				Keys = ["Recycling"],
			},
			new()
			{
				Name = "Food Waste",
				Colour = BinColour.Brown,
				Keys = ["Food", "Recycling"],
				Type = BinType.Caddy,
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Green,
				Keys = ["Garden"],
			},
		}.AsReadOnly();
	}
}
