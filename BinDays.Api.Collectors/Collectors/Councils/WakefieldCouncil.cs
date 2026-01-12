namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Web;

/// <summary>
/// Collector implementation for Wakefield Council.
/// </summary>
internal sealed partial class WakefieldCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Wakefield Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.wakefield.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "wakefield";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes = [
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "Household waste", "General waste", "Refuse" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Brown,
			Keys = [ "Mixed recycling", "Recycling" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden waste" ],
		},
	];

	/// <summary>
	/// Regex for the addresses from the address picker links.
	/// </summary>
	[GeneratedRegex(@"<a[^>]*?href=""(?<href>[^""]*?where-i-live\?uprn=[^""]+)""[^>]*>(?<label>[^<]+)<\/a>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the bin day panels on the property page.
	/// </summary>
	[GeneratedRegex(@"<div class=""u-mb-4""><strong>(?<service>[^<]+)<\/strong><\/div>[\s\S]*?Next collection - (?<next>[^<]+)<\/div>[\s\S]*?<ul class=""u-mt-4"">(?<future>[\s\S]*?)<\/ul>")]
	private static partial Regex BinDaysRegex();

	/// <summary>
	/// Regex for dates within the bin day panels.
	/// </summary>
	[GeneratedRegex(@"(?<date>[A-Za-z]+,\s+\d{1,2}\s+[A-Za-z]+\s+\d{4})")]
	private static partial Regex DateRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for base page (sets affinity cookies)
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://www.wakefield.gov.uk/where-i-live",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
			};

			return new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest
			};
		}
		// Prepare client-side request for address list
		else if (clientSideResponse.RequestId == 1)
		{
			var formattedPostcode = ProcessingUtilities.FormatPostcode(postcode);
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
				clientSideResponse.Headers.GetValueOrDefault("set-cookie") ?? string.Empty
			);

			var requestHeaders = new Dictionary<string, string>
			{
				{ "user-agent", Constants.UserAgent },
			};

			if (!string.IsNullOrWhiteSpace(requestCookies))
			{
				requestHeaders.Add("cookie", requestCookies);
			}

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"https://www.wakefield.gov.uk/pick-your-address?where-i-live={Uri.EscapeDataString(formattedPostcode)}",
				Method = "GET",
				Headers = requestHeaders,
			};

			return new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest
			};
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 2)
		{
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var href = HttpUtility.HtmlDecode(rawAddress.Groups["href"].Value);
				var normalisedHref = href.Replace(" ", "%20");
				var addressUri = normalisedHref.StartsWith("http", StringComparison.OrdinalIgnoreCase)
					? new Uri(normalisedHref)
					: new Uri(new Uri("https://www.wakefield.gov.uk"), normalisedHref);
				var queryParameters = HttpUtility.ParseQueryString(addressUri.Query);

				var uprn = queryParameters["uprn"]!;
				var property = queryParameters["a"] ?? rawAddress.Groups["label"].Value;
				var usrn = queryParameters["usrn"];
				var postcodeValue = ProcessingUtilities.FormatPostcode(queryParameters["p"] ?? postcode);

				var address = new Address
				{
					Property = property,
					Street = usrn,
					Postcode = postcodeValue,
					Uid = uprn,
				};

				addresses.Add(address);
			}

			return new GetAddressesResponse
			{
				Addresses = [.. addresses],
			};
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for bin collections page
		if (clientSideResponse == null)
		{
			var queryParameters = new List<string>
			{
				$"uprn={address.Uid}",
				$"a={Uri.EscapeDataString(address.Property!)}",
			};

			if (!string.IsNullOrWhiteSpace(address.Street))
			{
				queryParameters.Add($"usrn={Uri.EscapeDataString(address.Street)}");
			}

			if (!string.IsNullOrWhiteSpace(address.Postcode))
			{
				var formattedPostcode = ProcessingUtilities.FormatPostcode(address.Postcode);
				queryParameters.Add($"p={Uri.EscapeDataString(formattedPostcode)}");
			}

			var requestUrl = $"https://www.wakefield.gov.uk/where-i-live?{string.Join("&", queryParameters)}";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = requestUrl,
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
			};

			return new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest
			};
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 1)
		{
			var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content)!;

			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["service"].Value;
				var nextCollection = rawBinDay.Groups["next"].Value.Trim();
				var futureCollections = rawBinDay.Groups["future"].Value;

				var matchingBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);
				var collectionDates = new HashSet<DateOnly>();

				if (!nextCollection.Contains("n/a", StringComparison.OrdinalIgnoreCase))
				{
					collectionDates.Add(DateOnly.ParseExact(nextCollection, "dddd, d MMMM yyyy", CultureInfo.InvariantCulture));
				}

				foreach (Match dateMatch in DateRegex().Matches(futureCollections))
				{
					collectionDates.Add(DateOnly.ParseExact(dateMatch.Groups["date"].Value, "dddd, d MMMM yyyy", CultureInfo.InvariantCulture));
				}

				foreach (var collectionDate in collectionDates)
				{
					binDays.Add(new BinDay
					{
						Address = address,
						Date = collectionDate,
						Bins = matchingBins,
					});
				}
			}

			return new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}
}
