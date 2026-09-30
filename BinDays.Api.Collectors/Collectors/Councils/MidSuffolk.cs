namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using System;
using System.Collections.Generic;

/// <summary>
/// Collector implementation for Mid Suffolk District Council.
/// </summary>
internal sealed class MidSuffolk : PlacecubeCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Mid Suffolk";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.midsuffolk.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "mid-suffolk";

	/// <inheritdoc/>
	protected override string BaseUrl => "https://www.midsuffolk.gov.uk";

	/// <inheritdoc/>
	protected override IReadOnlyCollection<Bin> BinTypes =>
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Refuse Collection (General Rubbish)" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling Collection" ],
		},
		new()
		{
			Name = "Paper and Card Recycling",
			Colour = BinColour.Green,
			Keys = [ "Paper And Card Collection" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "Food Waste Collection" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden Waste Collection (Brown Bin)" ],
		},
		new()
		{
			Name = "Clinical Waste",
			Colour = BinColour.Yellow,
			Keys = [ "Clinical Waste" ],
		},
	];
}
