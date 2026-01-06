namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors.Vendors;
	using BinDays.Api.Collectors.Models;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Text.Json;

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
		protected override ReadOnlyCollection<Bin> BinTypes => NorthBinTypes;

		/// <summary>
		/// North Buckinghamshire Council (Aylesbury Vale) bin types.
		/// </summary>
		private static readonly ReadOnlyCollection<Bin> NorthBinTypes = new List<Bin>()
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

		/// <summary>
		/// South Buckinghamshire Council (Chiltern, South Bucks, Wycombe) bin types.
		/// </summary>
		private static readonly ReadOnlyCollection<Bin> SouthBinTypes = new List<Bin>()
		{
			new()
			{
				Name = "Food Waste",
				Colour = BinColour.Brown,
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
				Colour = BinColour.Green,
				Keys = new List<string>() { "Garden waste" }.AsReadOnly(),
			},
			new()
			{
				Name = "General Waste",
				Colour = BinColour.Black,
				Keys = new List<string>() { "General waste" }.AsReadOnly(),
			},
			new()
			{
				Name = "Paper and Cardboard",
				Colour = BinColour.Black,
				Keys = new List<string>() { "Paper and cardboard" }.AsReadOnly(),
				Type = BinType.Box,
			},
			new()
			{
				Name = "Textiles, Batteries and Electricals",
				Colour = BinColour.White,
				Keys = new List<string>() { "Textiles/Batteries/Electricals" }.AsReadOnly(),
				Type = BinType.Bag,
			},
		}.AsReadOnly();

		/// <inheritdoc/>
		protected override ReadOnlyCollection<Bin> GetBinTypes(Address address)
		{
			// Aylesbury Vale (North) consistently uses 9-digit UPRNs.
			// South areas (Chiltern, South Bucks, Wycombe) consistently use 11 or 12 digit UPRNs.
			return address.Uid!.Length > 9 ? SouthBinTypes : NorthBinTypes;
		}
	}
}
