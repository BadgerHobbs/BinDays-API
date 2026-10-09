namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using System;
using System.Collections.Generic;

/// <summary>
/// Collector implementation for Stirling Council.
/// </summary>
internal sealed class StirlingCouncil : RecollectCollectorBase, ICollector
{
	/// <inheritdoc/>
	public override string Name => "Stirling Council";

	/// <inheritdoc/>
	public override Uri WebsiteUrl => new("https://www.stirling.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "stirling";

	/// <inheritdoc/>
	protected override string AreaName => "StirlingUK";

	/// <inheritdoc/>
	protected override string ServiceId => "50013";

	/// <inheritdoc/>
	protected override IReadOnlyCollection<Bin> BinTypes =>
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Grey,
			Keys = [ "REFUSE" ],
		},
		new()
		{
			Name = "Paper & Cardboard Recycling",
			Colour = BinColour.Green,
			Keys = [ "RECYCLING" ],
		},
		new()
		{
			Name = "Plastic, Cans & Cartons Recycling",
			Colour = BinColour.Blue,
			Keys = [ "PLASTIC" ],
		},
		new()
		{
			Name = "Glass Recycling",
			Colour = BinColour.Blue,
			Keys = [ "GLASS" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Food & Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "GARDEN" ],
		},
	];
}
