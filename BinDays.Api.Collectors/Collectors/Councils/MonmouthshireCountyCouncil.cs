namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Monmouthshire County Council.
/// </summary>
internal sealed partial class MonmouthshireCountyCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Monmouthshire County Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.monmouthshire.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "monmouthshire";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	/// <remarks>
	/// The red and purple recycling bags are collected together and listed as a single service.
	/// </remarks>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Household rubbish" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Paper & Cardboard Recycling",
			Colour = BinColour.Red,
			Keys = [ "recycling bags" ],
			Type = BinType.Sack,
		},
		new()
		{
			Name = "Plastics, Cans & Cartons Recycling",
			Colour = BinColour.Purple,
			Keys = [ "recycling bags" ],
			Type = BinType.Sack,
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Blue,
			Keys = [ "food bin" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Glass Recycling",
			Colour = BinColour.Green,
			Keys = [ "Glass Box" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Nappy & Hygiene Waste",
			Colour = BinColour.Yellow,
			Keys = [ "hygiene waste bag" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Garden Waste" ],
		},
	];

	/// <summary>
	/// Regex for the services and their next collection dates from the waste collections panel.
	/// </summary>
	[GeneratedRegex(@"<h4>(?<service>[^<]+)</h4>(?:(?!<h4>).)*?Your\s+next\s+collection\s+date\s+is:?\s*<strong>(?<date>[^<]+)</strong>", RegexOptions.Singleline)]
	private static partial Regex BinDaysRegex();

	/// <summary>
	/// Regex for runs of whitespace, as the page wraps lines within service names and dates.
	/// </summary>
	[GeneratedRegex(@"\s+")]
	private static partial Regex WhitespaceRegex();

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
				Url = $"https://maps.monmouthshire.gov.uk/getdata.aspx?type=json&service=LocationSearch&RequestType=LocationSearch&location={postcode}&pagesize=100&startnum=1&mapsource=Monmouthshire/MyHouse",
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
				var address = new Address
				{
					Property = WhitespaceRegex().Replace(entry[7].GetString()!, " ").Trim(),
					Postcode = postcode,
					Uid = entry[0].GetString()!,
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
				Url = $"https://maps.monmouthshire.gov.uk/?action=SetAddress&UniqueId={address.Uid}",
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
		// Prepare client-side request for getting the local info page
		else if (clientSideResponse.RequestId == 1)
		{
			var cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://maps.monmouthshire.gov.uk/localinfo.aspx",
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

			// Iterate through each service, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = WebUtility.HtmlDecode(WhitespaceRegex().Replace(rawBinDay.Groups["service"].Value, " ")).Trim();
				var dateString = OrdinalSuffixRegex().Replace(WhitespaceRegex().Replace(rawBinDay.Groups["date"].Value, " ").Trim(), string.Empty);

				var binDay = new BinDay
				{
					Date = DateUtilities.ParseDateInferringYear(dateString, "dddd d MMMM"),
					Address = address,
					Bins = ProcessingUtilities.GetMatchingBins(_binTypes, service),
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
