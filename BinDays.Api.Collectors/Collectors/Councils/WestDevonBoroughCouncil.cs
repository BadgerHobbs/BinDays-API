namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors.Vendors;
	using BinDays.Api.Collectors.Models;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;

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
		protected override ReadOnlyCollection<Bin> BinTypes => new List<Bin>()
		{
			new()
			{
				Name = "Plastic & Metal Recycling",
				Colour = BinColour.White,
				Keys = new List<string>() { "Recycling and Food" }.AsReadOnly(),
				Type = BinType.Sack,
			},
			new()
			{
				Name = "Paper, Glass, & Cartons Recycling",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Recycling and Food" }.AsReadOnly(),
				Type = BinType.Box,
			},
			new()
			{
				Name = "Cardboard, Batteries, Ink, & Clothes Recycling",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Recycling and Food" }.AsReadOnly(),
				Type = BinType.Box,
			},
			new()
			{
				Name = "Food Waste",
				Colour = BinColour.Grey,
				Keys = new List<string>() { "Recycling and Food" }.AsReadOnly(),
				Type = BinType.Caddy,
			},
			new()
			{
				Name = "General Waste",
				Colour = BinColour.Brown,
				Keys = new List<string>() { "Refuse" }.AsReadOnly(),
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Garden" }.AsReadOnly(),
				Type = BinType.Sack,
			},
		}.AsReadOnly();
	}
}