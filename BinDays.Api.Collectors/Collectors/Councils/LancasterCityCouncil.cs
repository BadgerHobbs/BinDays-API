namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Lancaster City Council.
/// </summary>
internal sealed partial class LancasterCityCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Lancaster City Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.lancaster.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "lancaster";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Grey,
			Keys = [ "Domestic Waste Collection Service" ],
		},
		new()
		{
			Name = "Plastics, Glass & Cans Recycling",
			Colour = BinColour.Red,
			Keys = [ "Recycling Red Collection Service" ],
		},
		new()
		{
			Name = "Paper & Cardboard Recycling",
			Colour = BinColour.Yellow,
			Keys = [ "Recycling Yellow Collection Service" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "Food Waste Collection Service" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Garden Waste Collection Service" ],
		},
	];

	/// <summary>
	/// The base URL for the council's Whitespace collection service.
	/// </summary>
	private const string _baseUrl = "https://lcc-wrp.whitespacews.com";

	/// <summary>
	/// Regex for the addresses and their session-scoped collection links from the address list.
	/// </summary>
	[GeneratedRegex(@"href=""(?<link>mop\.php\?Track=[^""]+&serviceID=A&seq=3&pIndex=\d+)""[^>]*>\s*(?<address>[^<]+?)\s*</a>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the collection dates and services from the collections page.
	/// </summary>
	[GeneratedRegex(@"fontsize12rem "">(?<date>\d{2}/\d{2}/\d{4})</p>\s*</li>\s*<li[^>]*>\s*<p[^>]*>(?<service>[^<]+)</p>")]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = CreateAddressSearchRequest(postcode);

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
				var property = WebUtility.HtmlDecode(rawAddress.Groups["address"].Value);

				// The address is used as the Uid, as the collection links are only valid for the current search
				var address = new Address
				{
					Property = property,
					Postcode = postcode,
					Uid = property,
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
		// Prepare client-side request for getting addresses, as the collection links are session-scoped
		if (clientSideResponse == null)
		{
			var clientSideRequest = CreateAddressSearchRequest(address.Postcode!);

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting bin days using the matching address's collection link
		else if (clientSideResponse.RequestId == 1)
		{
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and find the collection link for the matching address
			var link = string.Empty;
			foreach (Match rawAddress in rawAddresses)
			{
				if (WebUtility.HtmlDecode(rawAddress.Groups["address"].Value) == address.Uid)
				{
					link = rawAddress.Groups["link"].Value;
					break;
				}
			}

			if (string.IsNullOrEmpty(link))
			{
				throw new InvalidOperationException("Unable to find matching address for this Uid.");
			}

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/{link}",
				Method = "GET",
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
				var service = rawBinDay.Groups["service"].Value.Trim();
				var date = DateUtilities.ParseDateExact(rawBinDay.Groups["date"].Value, "dd/MM/yyyy");

				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

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

	/// <summary>
	/// Creates the client-side request for searching addresses by postcode.
	/// </summary>
	private static ClientSideRequest CreateAddressSearchRequest(string postcode)
	{
		var clientSideRequest = new ClientSideRequest
		{
			RequestId = 1,
			Url = $"{_baseUrl}/mop.php?serviceID=A&seq=2",
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", Constants.FormUrlEncoded },
			},
			Body = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "address_postcode", postcode },
			}),
		};

		return clientSideRequest;
	}
}
