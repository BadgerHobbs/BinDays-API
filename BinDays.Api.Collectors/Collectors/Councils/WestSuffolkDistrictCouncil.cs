namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for West Suffolk District Council.
/// </summary>
internal sealed partial class WestSuffolkDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "West Suffolk District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://maps.westsuffolk.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "west-suffolk";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Black Bins" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Blue Bins" ],
		},
		new()
		{
			Name = "Paper & Card Recycling",
			Colour = BinColour.Green,
			Keys = [ "Green Bins" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Brown Bins" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "Food Bins" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// Regex for the bin collection entries from the HTML response.
	/// </summary>
	[GeneratedRegex(@"<strong>(?<type>[^:]+):</strong>\s*(?<date>(?:Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday) \d+(?:st|nd|rd|th)?\s+(?:January|February|March|April|May|June|July|August|September|October|November|December))", RegexOptions.Singleline)]
	private static partial Regex BinDaysRegex();

	/// <summary>
	/// Regex for removing ordinal suffixes from date strings.
	/// </summary>
	[GeneratedRegex(@"(?<=\d)(st|nd|rd|th)")]
	private static partial Regex OrdinalSuffixRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://maps.westsuffolk.gov.uk/getdata.aspx?service=LocationSearch&RequestType=LocationSearch&location={postcode}&pagesize=100&startnum=1&mapsource=mapsources/MyHouse",
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
			var dataArray = jsonDoc.RootElement.GetProperty("data");

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var entry in dataArray.EnumerateArray())
			{
				var uid = entry[0].GetString()!;
				var displayName = entry[7].GetString()!;

				var address = new Address
				{
					Property = displayName.Trim(),
					Postcode = postcode,
					Uid = uid,
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
		// Prepare client-side request for setting the address in the session
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://maps.westsuffolk.gov.uk/?action=SetAddress&UniqueId={address.Uid}",
				Method = "GET",
				Options = new ClientSideOptions
				{
					FollowRedirects = false,
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting the bin days page
		else if (clientSideResponse.RequestId == 1)
		{
			var cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
				clientSideResponse.Headers["set-cookie"]
			);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://maps.westsuffolk.gov.uk/MyWestSuffolk.aspx",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "cookie", cookie },
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 2)
		{
			var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var rawType = rawBinDay.Groups["type"].Value;
				var type = string.Join(" ", rawType.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));

				var rawDate = string.Join(" ", rawBinDay.Groups["date"].Value.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
				var dateString = OrdinalSuffixRegex().Replace(rawDate, "");
				var date = DateUtilities.ParseDateInferringYear(dateString, "dddd d MMMM");

				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, type);

				var binDay = new BinDay
				{
					Date = date,
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
