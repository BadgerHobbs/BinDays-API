namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using System;
using System.Collections.Generic;

/// <summary>
/// Collector implementation for South Ayrshire Council.
/// </summary>
internal sealed class SouthAyrshireCouncil : MyBinsAppCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "South Ayrshire Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.south-ayrshire.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "south-ayrshire";

	/// <inheritdoc/>
	protected override int AuthorityId => 28;

	/// <inheritdoc/>
	protected override IReadOnlyCollection<Bin> BinTypes { get; } =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Type = BinType.Bin,
			Keys = [ "Green Bin" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Blue,
			Type = BinType.Bin,
			Keys = [ "Blue Bin" ],
		},
		new()
		{
			Name = "Paper and Cardboard",
			Colour = BinColour.Grey,
			Type = BinType.Bin,
			Keys = [ "Grey Bin" ],
		},
		new()
		{
			Name = "Glass",
			Colour = BinColour.Purple,
			Type = BinType.Bin,
			Keys = [ "Purple Bin" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Type = BinType.Bin,
			Keys = [ "Brown Bin" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Type = BinType.Caddy,
			Keys = [ "Food Caddy" ],
		},
	];
}
