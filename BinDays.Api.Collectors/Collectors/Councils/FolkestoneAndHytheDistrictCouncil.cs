namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Folkestone and Hythe District Council.
/// </summary>
internal sealed partial class FolkestoneAndHytheDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Folkestone and Hythe District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.folkestone-hythe.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "folkestone-hythe";

	/// <summary>
	/// The list of bin types for this collector, matched on the container icon shown for each collection.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "icon_domestic_waste_bin" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Purple,
			Keys = [ "icon_recycling_bin" ],
		},
		new()
		{
			Name = "Paper & Card Recycling",
			Colour = BinColour.Black,
			Keys = [ "icon_paper_box" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "icon_food_waste_bin" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "icon_brown_lidded_bin" ],
		},
		new()
		{
			Name = "Communal General Waste",
			Colour = BinColour.Green,
			Keys = [ "icon_communal_domestic_waste_bin" ],
			Type = BinType.Container,
		},
		new()
		{
			Name = "Communal Mixed Recycling",
			Colour = BinColour.Purple,
			Keys = [ "icon_communal_purple_lidded_bin" ],
		},
		new()
		{
			Name = "Communal Paper & Card Recycling",
			Colour = BinColour.Black,
			Keys = [ "icon_communal_black_lidded_bin" ],
		},
		new()
		{
			Name = "Communal Food Waste",
			Colour = BinColour.Green,
			Keys = [ "icon_communal_green_lidded_bin" ],
		},
	];

	/// <summary>
	/// Regex for the addresses from the address select options.
	/// </summary>
	[GeneratedRegex(@"<option\s+class='[^']*'\s+value\s*=\s*'(?<uid>\d+)'>(?<address>[^<]+)</option>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the bin days from the collection cards, kept within a single card.
	/// </summary>
	[GeneratedRegex(@"<img src=""images/theme/default/(?<icon>[a-z_]+)\.png""(?:(?!</article>).)*?<h3 class=""service-title"">(?<service>[^<]+)</h3>(?:(?!</article>).)*?<p class=""service-next""><strong>[^<]*</strong><time datetime=""(?<date>\d{4}-\d{2}-\d{2})", RegexOptions.Singleline)]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://service.folkestone-hythe.gov.uk/webapp/myarea/?searchFor={postcode}",
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
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var address = new Address
				{
					Property = WebUtility.HtmlDecode(rawAddress.Groups["address"].Value).Trim(),
					Postcode = postcode,
					Uid = rawAddress.Groups["uid"].Value,
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
		// Prepare client-side request for getting the session cookie, which the collections endpoint requires
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://service.folkestone-hythe.gov.uk/webapp/myarea/",
				Method = "GET",
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
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"https://service.folkestone-hythe.gov.uk/webapp/myarea/api_collections.php?uprn={address.Uid}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "cookie", requestCookies },
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
				// Sack deliveries are listed alongside the collections, but are not a collection
				if (rawBinDay.Groups["service"].Value == "Refuse Bags Delivery")
				{
					continue;
				}

				var icon = rawBinDay.Groups["icon"].Value;
				var date = DateUtilities.ParseDateExact(rawBinDay.Groups["date"].Value, "yyyy-MM-dd");

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, icon);

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
