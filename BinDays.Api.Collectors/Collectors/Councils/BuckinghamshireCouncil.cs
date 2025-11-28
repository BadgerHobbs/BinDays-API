namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors.Vendors;
	using BinDays.Api.Collectors.Models;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;

	/// <summary>
	/// Collector implementation for Buckinghamshire Council.
	/// </summary>
	internal sealed class BuckinghamshireCouncil : ITouchVisionCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Buckinghamshire Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.buckinghamshire.gov.uk/waste-and-recycling/find-out-when-its-your-bin-collection/");

		/// <inheritdoc/>
		public override string GovUkId => "buckinghamshire";

		/// <inheritdoc/>
		protected override int ClientId => 152;

		/// <inheritdoc/>
		protected override int CouncilId => 34505;

		/// <inheritdoc/>
		protected override string ApiBaseUrl => "https://itouchvision.app/portal/itouchvision/";

		/// <inheritdoc/>
		protected override ReadOnlyCollection<Bin> BinTypes => new List<Bin>()
		{
			new()
			{
				Name = "Food Waste",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Food waste" }.AsReadOnly(),
				Type = BinType.Caddy,
			},
			new()
			{
				Name = "Mixed Recycling",
				Colour = BinColour.Blue,
				Keys = new List<string>() { "Mixed recycling" }.AsReadOnly(),
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Brown,
				Keys = new List<string>() { "Garden waste" }.AsReadOnly(),
			},
			new()
			{
				Name = "General Waste",
				Colour = BinColour.Green,
				Keys = new List<string>() { "General waste" }.AsReadOnly(),
			},
		}.AsReadOnly();
	}
}