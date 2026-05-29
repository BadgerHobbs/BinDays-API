namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Ipswich Borough Council.
/// </summary>
internal sealed partial class IpswichBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Ipswich Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.ipswich.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "ipswich";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "Large Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "Large food waste caddy" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Blue Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Blue recycling bin" ],
		},
		new()
		{
			Name = "Green-Lidded Recycling",
			Colour = BinColour.Green,
			Keys = [ "Green-lidded recycling bin" ],
		},
		new()
		{
			Name = "Black Refuse",
			Colour = BinColour.Black,
			Keys = [ "Black refuse bin" ],
		},
		new()
		{
			Name = "Brown Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Brown garden waste bin" ],
		},
	];

	/// <summary>
	/// The base URL for Ipswich's bin collection pages.
	/// </summary>
	private const string _baseUrl = "https://app.ipswich.gov.uk";

	/// <summary>
	/// Regex for the addresses from the data.
	/// </summary>
	[GeneratedRegex(@"<li>\s*<a href=""/(?<path>bin-collection(?:-better-recycling)?)/weeks/(?<uid>\d+)"">(?<street>[^<]+)</a>\s*</li>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for each bin day entry from the data.
	/// </summary>
	[GeneratedRegex(@"<dt class=""ibc-calendar-entry"">[\s\S]*?<div class=""ibc-calendar-entry__date"">(?<day>\d+)<span class=""ibc-visually-hidden"">[^<]+</span></div>\s*<div class=""ibc-calendar-entry__month"">(?<monthYear>[^<]+)</div>[\s\S]*?<dd class=""ibc-calendar-entry__details"">[\s\S]*?<ul>\s*(?<bins>[\s\S]*?)\s*</ul>")]
	private static partial Regex BinDaysRegex();

	/// <summary>
	/// Regex for each bin type in a bin day entry.
	/// </summary>
	[GeneratedRegex(@"<li class=""[^""]+"">(?<service>[^<]+)</li>")]
	private static partial Regex BinNameRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
		{
			{ "street-input", postcode },
			{ "submit-button", string.Empty },
		});

		// Prepare client-side request for getting addresses (standard path)
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/bin-collection/",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
				},
				Body = requestBody,
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from standard path; fall back to better-recycling path if none found
		else if (clientSideResponse.RequestId == 1)
		{
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

			if (rawAddresses.Count == 0)
			{
				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 2,
					Url = $"{_baseUrl}/bin-collection-better-recycling/",
					Method = "POST",
					Headers = new()
					{
						{ "user-agent", Constants.UserAgent },
						{ "content-type", Constants.FormUrlEncoded },
					},
					Body = requestBody,
				};

				var getAddressesResponse = new GetAddressesResponse
				{
					NextClientSideRequest = clientSideRequest,
				};

				return getAddressesResponse;
			}

			return ParseAddressesResponse(postcode, rawAddresses);
		}
		// Process addresses from better-recycling path
		else if (clientSideResponse.RequestId == 2)
		{
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;
			return ParseAddressesResponse(postcode, rawAddresses);
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
			// Uid format: "path;uid" (e.g., "bin-collection-better-recycling;12345")
			string path;
			string uid;

			if (address.Uid!.Contains(';'))
			{
				var parts = address.Uid.Split(';', 2);
				path = parts[0];
				uid = parts[1];
			}
			else
			{
				// TODO: Remove once legacy UIDs are no longer in circulation
				path = "bin-collection-better-recycling";
				uid = address.Uid;
			}

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/{path}/weeks/{uid}",
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
			var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var day = rawBinDay.Groups["day"].Value.Trim();
				var monthYear = rawBinDay.Groups["monthYear"].Value.Trim();
				var date = DateUtilities.ParseDateExact(
					$"{day} {monthYear}",
					"d MMMM yyyy"
				);

				var rawBins = BinNameRegex().Matches(rawBinDay.Groups["bins"].Value)!;

				// Iterate through each bin for the current date, and create a new bin day object
				foreach (Match rawBin in rawBins)
				{
					var service = WebUtility.HtmlDecode(rawBin.Groups["service"].Value.Trim());
					var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

					if (matchedBins.Count == 0)
					{
						continue;
					}

					var binDay = new BinDay
					{
						Date = date,
						Address = address,
						Bins = matchedBins,
					};

					binDays.Add(binDay);
				}
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

	/// <summary>
	/// Parses address matches into an addresses response, storing the URL path in each UID.
	/// </summary>
	private static GetAddressesResponse ParseAddressesResponse(string postcode, MatchCollection rawAddresses)
	{
		// Iterate through each address, and create a new address object
		var addresses = new List<Address>();
		foreach (Match rawAddress in rawAddresses)
		{
			var street = WebUtility.HtmlDecode(rawAddress.Groups["street"].Value.Trim());
			var path = rawAddress.Groups["path"].Value.Trim();
			var uid = rawAddress.Groups["uid"].Value.Trim();

			var address = new Address
			{
				Property = street,
				Postcode = postcode,
				Uid = $"{path};{uid}",
			};

			addresses.Add(address);
		}

		var getAddressesResponse = new GetAddressesResponse
		{
			Addresses = [.. addresses],
		};

		return getAddressesResponse;
	}
}
