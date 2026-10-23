namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for South Derbyshire District Council.
/// </summary>
internal sealed partial class SouthDerbyshireDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "South Derbyshire District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.southderbyshire.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "south-derbyshire";

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
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "Food" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// The URL of the council's iShare data service.
	/// </summary>
	private const string _dataUrl = "https://maps.southderbyshire.gov.uk/iShareLIVE.web/getdata.aspx";

	/// <summary>
	/// Regex for the collection dates and the bins collected on them from the next bin collections HTML.
	/// </summary>
	[GeneratedRegex(@"font-size:22px[^>]*>(?<date>[^<]+)</span><span[^>]*>(?<bins>[^<]+)</span>")]
	private static partial Regex BinDaysRegex();

	/// <summary>
	/// Regex for runs of whitespace, as the council's addresses contain repeated spaces.
	/// </summary>
	[GeneratedRegex(@"\s+")]
	private static partial Regex WhitespaceRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_dataUrl}?RequestType=LocationSearch&location={postcode}&pagesize=100&startnum=1&mapsource=mapsources/MyHouse",
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
			var addresses = new List<Address>();

			// Some postcodes outside the district return an empty response rather than an empty list
			if (clientSideResponse.Content.Length > 0)
			{
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

				// Iterate through each address, and create a new address object
				foreach (var rawAddress in jsonDoc.RootElement.GetProperty("data").EnumerateArray())
				{
					var address = new Address
					{
						Property = WhitespaceRegex().Replace(rawAddress[7].GetString()!, " ").Trim(),
						Postcode = postcode,
						Uid = rawAddress[0].GetString()!,
					};

					addresses.Add(address);
				}
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
				Url = $"{_dataUrl}?RequestType=LocalInfo&ms=mapsources/MyHouse&format=JSON&group=Recycling%20Bins%20and%20Waste|Next%20Bin%20Collections&uid={address.Uid}",
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
			var collectionsHtml = jsonDoc.RootElement
				.GetProperty("Results")
				.GetProperty("Next_Bin_Collections")
				.GetProperty("_")
				.GetString()!;

			var rawBinDays = BinDaysRegex().Matches(collectionsHtml)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				// Parse the date (e.g. "28 October 2026")
				var date = DateUtilities.ParseDateExact(rawBinDay.Groups["date"].Value.Trim(), "dd MMMM yyyy");

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, rawBinDay.Groups["bins"].Value);

				var binDay = new BinDay
				{
					Date = date,
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
