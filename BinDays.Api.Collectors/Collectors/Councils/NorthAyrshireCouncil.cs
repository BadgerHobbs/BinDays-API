namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

/// <summary>
/// Collector implementation for North Ayrshire Council.
/// </summary>
internal sealed class NorthAyrshireCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "North Ayrshire Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.north-ayrshire.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "north-ayrshire";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Grey,
			Keys = [ "GREY_DATE_TEXT" ],
		},
		new()
		{
			Name = "Paper and Card Recycling",
			Colour = BinColour.Blue,
			Keys = [ "BLUE_DATE_TEXT" ],
		},
		new()
		{
			Name = "Glass, Metals and Plastic Recycling",
			Colour = BinColour.Purple,
			Keys = [ "PURPLE_DATE_TEXT" ],
		},
		new()
		{
			Name = "Garden and Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "BROWN_DATE_TEXT" ],
		},
	];

	/// <summary>
	/// The base URL for North Ayrshire ArcGIS map services.
	/// </summary>
	private const string _mapsApiBaseUrl = "https://www.maps.north-ayrshire.gov.uk/arcgis/rest/services";

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_mapsApiBaseUrl}/AGOL/CAG_VIEW/MapServer/0/query?f=json&outFields=ADDRESS,UPRN&returnGeometry=false&where=UPPER(ADDRESS)%20LIKE%20'%25{postcode}%25'",
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rawAddresses = jsonDoc.RootElement.GetProperty("features");

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in rawAddresses.EnumerateArray())
			{
				var attributes = rawAddress.GetProperty("attributes");
				var uprn = long.Parse(
					attributes.GetProperty("UPRN").GetString()!,
					CultureInfo.InvariantCulture
				).ToString(CultureInfo.InvariantCulture);

				var address = new Address
				{
					Property = attributes.GetProperty("ADDRESS").GetString()!.Trim(),
					Postcode = postcode,
					Uid = uprn,
				};

				addresses.Add(address);
			}

			var getAddressesResponse = new GetAddressesResponse
			{
				Addresses = [.. addresses],
			};

			return getAddressesResponse;
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting bin days
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_mapsApiBaseUrl}/AGOL/YourLocationLive/MapServer/8/query?f=json&outFields=BLUE_DATE_TEXT,GREY_DATE_TEXT,PURPLE_DATE_TEXT,BROWN_DATE_TEXT&returnGeometry=false&where=UPRN%20=%20'{address.Uid!}'",
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var attributes = jsonDoc.RootElement.GetProperty("features")[0].GetProperty("attributes");

			var rawCollections = new[]
			{
				(Service: "BROWN_DATE_TEXT", Date: attributes.GetProperty("BROWN_DATE_TEXT").GetString()),
				(Service: "GREY_DATE_TEXT", Date: attributes.GetProperty("GREY_DATE_TEXT").GetString()),
				(Service: "BLUE_DATE_TEXT", Date: attributes.GetProperty("BLUE_DATE_TEXT").GetString()),
				(Service: "PURPLE_DATE_TEXT", Date: attributes.GetProperty("PURPLE_DATE_TEXT").GetString()),
			};

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var (Service, Date) in rawCollections)
			{
				if (string.IsNullOrWhiteSpace(Date))
				{
					continue;
				}

				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, Service);

				var binDay = new BinDay
				{
					Date = DateUtilities.ParseDateExact(Date, "dd/MM/yyyy"),
					Address = address,
					Bins = matchedBins,
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
