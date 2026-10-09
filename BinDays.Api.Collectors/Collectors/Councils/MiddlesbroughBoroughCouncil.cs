namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using System;
using System.Collections.Generic;

/// <summary>
/// Collector implementation for Middlesbrough Borough Council.
/// </summary>
internal sealed class MiddlesbroughBoroughCouncil : RecollectCollectorBase, ICollector
{
	/// <inheritdoc/>
	public override string Name => "Middlesbrough Borough Council";

	/// <inheritdoc/>
	public override Uri WebsiteUrl => new("https://www.middlesbrough.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "middlesbrough";

	/// <inheritdoc/>
	protected override string AreaName => "MiddlesbroughUK";

	/// <inheritdoc/>
	protected override string ServiceId => "50005";

	/// <inheritdoc/>
	protected override IReadOnlyCollection<Bin> BinTypes =>
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "REFUSE" ],
		},
		new()
		{
			Name = "Paper & Card Recycling",
			Colour = BinColour.Red,
			Keys = [ "RECYCLING" ],
		},
		new()
		{
			Name = "Plastic, Glass, Cans & Cartons Recycling",
			Colour = BinColour.Blue,
			Keys = [ "PLASTIC" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "FOOD" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "GARDEN" ],
		},
	];
}
