namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using System;
using System.Collections.Generic;

/// <summary>
/// Collector implementation for Caerphilly County Borough Council.
/// </summary>
internal sealed class CaerphillyCountyBoroughCouncil : RecollectCollectorBase, ICollector
{
	/// <inheritdoc/>
	public override string Name => "Caerphilly County Borough Council";

	/// <inheritdoc/>
	public override Uri WebsiteUrl => new("https://www.caerphilly.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "caerphilly";

	/// <inheritdoc/>
	protected override string AreaName => "CaerphillyCountyUK";

	/// <inheritdoc/>
	protected override string ServiceId => "50008";

	/// <inheritdoc/>
	protected override IReadOnlyCollection<Bin> BinTypes =>
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "REFUSE" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Brown,
			Keys = [ "RECYCLING" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "FOOD" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Any,
			Keys = [ "FOOD" ],
			Type = BinType.Sack,
		},
	];
}
