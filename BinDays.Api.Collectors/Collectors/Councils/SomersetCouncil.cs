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
				Keys = new List<string>() { "Rubbish" }.AsReadOnly(),
			},
			new()
			{
				Name = "Recycling",
				Colour = BinColour.Blue,
				Keys = new List<string>() { "Recycling" }.AsReadOnly(),
			},
			new()
			{
				Name = "Food Waste",
				Colour = BinColour.Brown,
				Keys = new List<string>() { "Food", "Recycling" }.AsReadOnly(),
				Type = BinType.Caddy,
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Garden" }.AsReadOnly(),
			},
		}.AsReadOnly();
	}
}
