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
	[GeneratedRegex(@"<li>\s*<a href=""/bin-collection(?:-better-recycling)?/weeks/(?<uid>\d+)"">(?<street>[^<]+)</a>\s*</li>")]
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
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "street-input", postcode },
				{ "submit-button", string.Empty },
			});

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
		// Process addresses from response
		else if (clientSideResponse.RequestId == 1)
		{
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var street = WebUtility.HtmlDecode(rawAddress.Groups["street"].Value.Trim());
				var uid = rawAddress.Groups["uid"].Value.Trim();

				var address = new Address
				{
					Property = street,
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
		// Prepare client-side request for getting bin days
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/bin-collection-better-recycling/weeks/{address.Uid!}",
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
}
