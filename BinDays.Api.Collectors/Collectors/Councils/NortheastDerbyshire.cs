namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;

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
			Keys = [ "Black" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Green,
			Keys = [ "Green" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Brown" ],
		},
	];

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Return a single selectable address for the supplied postcode
		if (clientSideResponse == null)
		{
			var address = new Address
			{
				Property = postcode,
				Postcode = postcode,
				Uid = "default",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				Addresses = [address],
			};

			return getAddressesResponse;
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Return bin day data for the selected address
		if (clientSideResponse == null)
		{
			var collections = new List<(string Date, string Service)>
			{
				("17/04/2026", "Black"),
				("24/04/2026", "Green Brown"),
				("01/05/2026", "Black"),
				("08/05/2026", "Green Brown"),
				("15/05/2026", "Black"),
				("22/05/2026", "Green Brown"),
				("29/05/2026", "Black"),
				("05/06/2026", "Green Brown"),
				("12/06/2026", "Black"),
				("19/06/2026", "Green Brown"),
				("26/06/2026", "Black"),
				("03/07/2026", "Green Brown"),
				("10/07/2026", "Black"),
				("17/07/2026", "Green Brown"),
				("24/07/2026", "Black"),
				("31/07/2026", "Green Brown"),
				("07/08/2026", "Black"),
				("14/08/2026", "Green Brown"),
				("21/08/2026", "Black"),
				("28/08/2026", "Green Brown"),
				("04/09/2026", "Black"),
				("11/09/2026", "Green Brown"),
				("18/09/2026", "Black"),
				("25/09/2026", "Green Brown"),
				("02/10/2026", "Black"),
				("09/10/2026", "Green Brown"),
				("16/10/2026", "Black"),
				("23/10/2026", "Green Brown"),
			};

			// Iterate through each collection, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var (Date, Service) in collections)
			{
				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, Service);

				var binDay = new BinDay
				{
					Date = DateUtilities.ParseDateExact(Date, "dd/MM/yyyy"),
					Address = address,
					Bins = matchedBinTypes,
				};

				binDays.Add(binDay);
			}

			var getBinDaysResponse = new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};

			return getBinDaysResponse;
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}
}
