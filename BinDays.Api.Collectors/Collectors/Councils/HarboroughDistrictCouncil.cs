namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using System;
using System.Collections.Generic;

/// <summary>
/// Collector implementation for Harborough District Council.
/// </summary>
internal sealed class HarboroughDistrictCouncil : FccCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Harborough District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://harborough.fccenvironment.co.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "harborough";

	/// <inheritdoc/>
	protected override string BaseUrl => "https://harborough.fccenvironment.co.uk/";

	/// <inheritdoc/>
	protected override string CollectionDetailsEndpoint => "ajaxprocessor/getcollectiondetails";

	/// <inheritdoc/>
	protected override IReadOnlyCollection<Bin> BinTypes =>
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Non-recyclable waste" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling collection" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Garden waste" ],
		},
	];
}
