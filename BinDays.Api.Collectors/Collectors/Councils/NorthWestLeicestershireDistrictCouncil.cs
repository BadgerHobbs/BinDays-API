namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for North West Leicestershire District Council.
/// </summary>
internal sealed partial class NorthWestLeicestershireDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "North West Leicestershire District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.nwleics.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "north-west-leicestershire";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Refuse" ],
		},
		new()
		{
			Name = "Glass, Cans & Plastics",
			Colour = BinColour.Red,
			Keys = [ "Red Box" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Paper Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Blue Bag" ],
			Type = BinType.Sack,
		},
		new()
		{
			Name = "Cardboard Recycling",
			Colour = BinColour.Yellow,
			Keys = [ "Yellow Bag" ],
			Type = BinType.Sack,
		},
		new()
		{
			Name = "Plastic Bags & Wrapping",
			Colour = BinColour.Purple,
			Keys = [ "Purple Bag" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "Food Waste" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden Waste" ],
		},
	];

	/// <summary>
	/// Regex for the collection dates and bin names from the refuse collection dates list.
	/// </summary>
	[GeneratedRegex(@"<strong class=""date"">(?<date>[^<]+)</strong><a[^>]*>(?<bin>[^<]+)</a>")]
	private static partial Regex BinDaysRegex();

	/// <summary>
	/// Regex for the ordinal suffixes on the collection dates.
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
				Url = $"https://my.nwleics.gov.uk/data/ac/addresses.json?term={postcode}",
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

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in jsonDoc.RootElement.EnumerateArray())
			{
				var address = new Address
				{
					Property = rawAddress.GetProperty("label").GetString()!,
					Postcode = postcode,
					Uid = rawAddress.GetProperty("value").GetString()!,
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
		// Prepare client-side request for setting the location cookie
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://my.nwleics.gov.uk/location?put={address.Uid}",
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
		// Prepare client-side request for getting bin days
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://my.nwleics.gov.uk/",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "cookie", cookies },
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
				// Parse the date (e.g. "Today", "Tomorrow" or "Tue 27th Oct")
				var dateString = OrdinalSuffixRegex().Replace(rawBinDay.Groups["date"].Value.Trim(), "");
				var date = DateUtilities.ParseRelativeDateOrInferYear(dateString, "ddd d MMM");

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, rawBinDay.Groups["bin"].Value);

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
