namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using System;
using System.Collections.Generic;

/// <summary>
/// Collector implementation for Redcar and Cleveland Borough Council.
/// </summary>
internal sealed class RedcarAndClevelandBoroughCouncil : RecollectCollectorBase, ICollector
{
	/// <inheritdoc/>
	public override string Name => "Redcar and Cleveland Borough Council";

	/// <inheritdoc/>
	public override Uri WebsiteUrl => new("https://www.redcar-cleveland.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "redcar-and-cleveland";

	/// <inheritdoc/>
	protected override string AreaName => "RedcarandClevelandUK";

	/// <inheritdoc/>
	protected override string ServiceId => "50006";

	/// <inheritdoc/>
	protected override IReadOnlyCollection<Bin> BinTypes =>
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Brown,
			Keys = [ "REFUSE" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Blue,
			Keys = [ "RECYCLING" ],
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
			Colour = BinColour.Green,
			Keys = [ "GARDEN" ],
		},
	];
}
