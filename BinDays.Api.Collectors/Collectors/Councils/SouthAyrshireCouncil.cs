namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using System;
using System.Collections.Generic;
using System.Linq;

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
	protected override IReadOnlyCollection<Bin> BinTypes =>
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

	/// <inheritdoc/>
	public new GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		var response = base.GetBinDays(address, clientSideResponse);

		// Filter out Brown Bin collections during winter months (December, January, February)
		if (response.BinDays != null)
		{
			var filteredBinDays = response.BinDays
				.Where(binDay =>
				{
					var isBrownBin = binDay.Bins.Any(bin => bin.Name == "Garden Waste");
					var isWinterMonth = binDay.Date.Month is 12 or 1 or 2;
					return !(isBrownBin && isWinterMonth);
				})
				.ToArray();

			return new GetBinDaysResponse
			{
				BinDays = filteredBinDays,
				NextClientSideRequest = response.NextClientSideRequest,
			};
		}

		return response;
	}
}
