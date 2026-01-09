namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using System.Collections.Generic;

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
	protected override IReadOnlyCollection<Bin> BinTypes => _northBinTypes;

	/// <summary>
	/// North Buckinghamshire Council (Aylesbury Vale) bin types.
	/// </summary>
	private static readonly IReadOnlyCollection<Bin> _northBinTypes = [
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Food waste" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Mixed recycling" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden waste" ],
		},
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "General waste" ],
		},
	];

	/// <summary>
	/// South Buckinghamshire Council (Chiltern, South Bucks, Wycombe) bin types.
	/// </summary>
	private static readonly IReadOnlyCollection<Bin> _southBinTypes = [
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "Food waste" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Mixed recycling" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Garden waste" ],
		},
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "General waste" ],
		},
		new()
		{
			Name = "Paper and Cardboard",
			Colour = BinColour.Black,
			Keys = [ "Paper and cardboard" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Textiles, Batteries and Electricals",
			Colour = BinColour.White,
			Keys = [ "Textiles/Batteries/Electricals" ],
			Type = BinType.Bag,
		},
	];

	/// <inheritdoc/>
	protected override IReadOnlyCollection<Bin> GetBinTypes(Address address)
	{
		// Aylesbury Vale (North) consistently uses 9-digit UPRNs.
		// South areas (Chiltern, South Bucks, Wycombe) consistently use 11 or 12 digit UPRNs.
		return address.Uid!.Length > 9 ? _southBinTypes : _northBinTypes;
	}
}
