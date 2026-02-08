namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for London Borough of Newham.
/// </summary>
internal sealed partial class LondonBoroughOfNewham : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "London Borough of Newham";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.newham.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "newham";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Domestic" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "Food Waste" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// The base URL for the bin collection service.
	/// </summary>
	private const string _baseUrl = "https://bincollection.newham.gov.uk/";

	/// <summary>
	/// Regex for the Salesforce secure form id.
	/// </summary>
	[GeneratedRegex(@"name=""as_sfid""[^>]+value=""(?<token>[^""]+)""", RegexOptions.IgnoreCase)]
	private static partial Regex AsSfidRegex();

	/// <summary>
	/// Regex for the Salesforce form id.
	/// </summary>
	[GeneratedRegex(@"name=""as_fid""[^>]+value=""(?<token>[^""]+)""", RegexOptions.IgnoreCase)]
	private static partial Regex AsFidRegex();

	/// <summary>
	/// Regex for the addresses from the search results table.
	/// </summary>
	[GeneratedRegex(@"<a[^>]+/Details/Index/(?<uid>[^""]+)""[^>]*>Select</a></td>\s*<td>(?<line1>[^<]*)</td>\s*<td>(?<line2>[^<]*)</td>\s*<td>(?<postcode>[^<]+)</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the bin days from the detail cards.
	/// </summary>
	[GeneratedRegex(@"<div class=""card-header"">Your <b>(?<service>[^<]+)</b> Collection Day</div>(?:(?!There are no).)*?<b>Next&nbsp;</b><mark>[^<]+</mark>&nbsp;(?<nextDate>\d{2}/\d{2}/\d{4})(?:.*?<b>Previous&nbsp;</b><mark>[^<]+</mark>&nbsp;(?<previousDate>\d{2}/\d{2}/\d{4}))?", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting the search form and tokens
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _baseUrl,
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for searching addresses
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var asSfid = AsSfidRegex().Match(clientSideResponse.Content).Groups["token"].Value;
			var asFid = AsFidRegex().Match(clientSideResponse.Content).Groups["token"].Value;

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "Address", postcode },
				{ "btnSearch", "Search" },
				{ "as_sfid", asSfid },
				{ "as_fid", asFid },
			});

			Dictionary<string, string> requestHeaders = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", "application/x-www-form-urlencoded" },
				{ "cookie", cookie },
			};

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = _baseUrl,
				Method = "POST",
				Headers = requestHeaders,
				Body = requestBody,
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 2)
		{
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var numericAddresses = new List<Address>();
			var otherAddresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var line1 = rawAddress.Groups["line1"].Value.Trim();
				var line2 = rawAddress.Groups["line2"].Value.Trim();

				var address = new Address
				{
					Property = line1,
					Street = line2,
					Postcode = postcode,
					Uid = rawAddress.Groups["uid"].Value,
				};

				var hasLeadingDigit = !string.IsNullOrWhiteSpace(line1) && char.IsDigit(line1[0]);

				if (hasLeadingDigit)
				{
					numericAddresses.Add(address);
				}
				else
				{
					otherAddresses.Add(address);
				}
			}

			var getAddressesResponse = new GetAddressesResponse
			{
				Addresses = [.. numericAddresses, .. otherAddresses],
			};

			return getAddressesResponse;
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting bin collections
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}Details/Index/{address.Uid}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
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

			// Iterate through each bin day, and create bin day objects
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["service"].Value.Trim();
				var bins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				var dateTexts = new[]
				{
					rawBinDay.Groups["nextDate"].Value.Trim(),
					rawBinDay.Groups["previousDate"].Value.Trim(),
				};

				foreach (var dateText in dateTexts)
				{
					if (string.IsNullOrWhiteSpace(dateText))
					{
						continue;
					}

					var date = DateOnly.ParseExact(
						dateText,
						"dd/MM/yyyy",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);

					var binDay = new BinDay
					{
						Address = address,
						Date = date,
						Bins = bins,
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

		throw new InvalidOperationException("Invalid client-side request.");
	}
}
