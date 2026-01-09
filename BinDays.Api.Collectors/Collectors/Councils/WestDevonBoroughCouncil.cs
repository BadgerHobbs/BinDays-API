namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors.Vendors;
	using BinDays.Api.Collectors.Models;
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Collector implementation for West Devon Borough Council.
	/// </summary>
	internal sealed class WestDevonBoroughCouncil : FccCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "West Devon Borough Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://westdevon.fccenvironment.co.uk/mycollections");

		/// <inheritdoc/>
		public override string GovUkId => "west-devon";

		/// <inheritdoc/>
		protected override string BaseUrl => "https://westdevon.fccenvironment.co.uk/";

		/// <inheritdoc/>
		protected override string CollectionDetailsEndpoint => "ajaxprocessor/getcollectiondetails";

		/// <inheritdoc/>
		protected override IReadOnlyCollection<Bin> BinTypes => [
			new()
			{
				Name = "Plastic & Metal Recycling",
				Colour = BinColour.White,
				Keys = [ "Recycling and Food" ],
				Type = BinType.Sack,
			},
			new()
			{
				Name = "Paper, Glass, & Cartons Recycling",
				Colour = BinColour.Green,
				Keys = [ "Recycling and Food" ],
				Type = BinType.Box,
			},
			new()
			{
				Name = "Cardboard, Batteries, Ink, & Clothes Recycling",
				Colour = BinColour.Green,
				Keys = [ "Recycling and Food" ],
				Type = BinType.Box,
			},
			new()
			{
				Name = "Food Waste",
				Colour = BinColour.Grey,
				Keys = [ "Recycling and Food" ],
				Type = BinType.Caddy,
			},
			new()
			{
				Name = "General Waste",
				Colour = BinColour.Brown,
				Keys = [ "Refuse" ],
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Green,
				Keys = [ "Garden" ],
				Type = BinType.Sack,
			},
		];
	}
}
