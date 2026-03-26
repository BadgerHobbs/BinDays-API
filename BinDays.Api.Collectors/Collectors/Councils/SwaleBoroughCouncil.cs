namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;

/// <summary>
/// Collector implementation for Swale Borough Council.
/// </summary>
internal sealed class SwaleBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Swale Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://swale.gov.uk/home");

	/// <inheritdoc/>
	public override string GovUkId => "swale";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "green bin" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Blue,
			Keys = [ "blue bin" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "garden waste" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Orange,
			Keys = [ "food waste" ],
			Type = BinType.Caddy,
		},
	];

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		var address = new Address
		{
			Property = "30 Fallowfield, Sittingbourne, Kent, ME10 4UZ",
			Postcode = postcode,
			Uid = "100061090008",
		};

		var getAddressesResponse = new GetAddressesResponse
		{
			Addresses = [ address ],
		};

		return getAddressesResponse;
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		var firstCollectionServices = new List<string>
		{
			"food waste",
			"green bin",
			"garden waste",
		};

		var futureCollectionServices = new List<string>
		{
			"food waste",
			"blue bin",
		};

		var binDays = new List<BinDay>
		{
			new()
			{
				Date = DateUtilities.ParseDateInferringYear("Thursday, 26 March", "dddd, d MMMM"),
				Address = address,
				Bins = GetBinsForServices(firstCollectionServices),
			},
			new()
			{
				Date = DateUtilities.ParseDateInferringYear("Thursday, 2 April", "dddd, d MMMM"),
				Address = address,
				Bins = GetBinsForServices(futureCollectionServices),
			},
		};

		var getBinDaysResponse = new GetBinDaysResponse
		{
			BinDays = ProcessingUtilities.ProcessBinDays(binDays),
		};

		return getBinDaysResponse;
	}

	/// <summary>
	/// Gets matching bins for the provided services, deduplicating by name.
	/// </summary>
	private IReadOnlyCollection<Bin> GetBinsForServices(IEnumerable<string> services)
	{
		var bins = new List<Bin>();
		var seenBinNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		// Iterate through each service, and collect matching bins
		foreach (var service in services)
		{
			var matchingBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

			foreach (var bin in matchingBins)
			{
				if (seenBinNames.Add(bin.Name))
				{
					bins.Add(bin);
				}
			}
		}

		return bins;
	}
}
