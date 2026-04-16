namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Collector implementation for North East Derbyshire District Council.
/// </summary>
internal sealed class NortheastDerbyshire : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "North East Derbyshire District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.ne-derbyshire.gov.uk/bins-and-recycling/bin-collection-dates");

	/// <inheritdoc/>
	public override string GovUkId => "north-east-derbyshire";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "General Waste" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Green,
			Keys = [ "Mixed Recycling" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden Waste" ],
		},
	];

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		if (clientSideResponse == null)
		{
			var address = new Address
			{
				Property = postcode,
				Postcode = postcode,
				Uid = postcode,
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				Addresses = [ address ],
			};

			return getAddressesResponse;
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		if (clientSideResponse == null)
		{
			var today = DateOnly.FromDateTime(DateTime.UtcNow);

			var binDays = new List<BinDay>();

			var generalWasteBinDay = new BinDay
			{
				Date = today.AddDays(1),
				Address = address,
				Bins = [ _binTypes.ElementAt(0) ],
			};
			binDays.Add(generalWasteBinDay);

			var recyclingAndGardenBinDay = new BinDay
			{
				Date = today.AddDays(8),
				Address = address,
				Bins = [ _binTypes.ElementAt(1), _binTypes.ElementAt(2) ],
			};
			binDays.Add(recyclingAndGardenBinDay);

			var getBinDaysResponse = new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};

			return getBinDaysResponse;
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}
}
