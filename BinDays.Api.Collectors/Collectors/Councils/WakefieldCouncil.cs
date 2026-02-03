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
	[GeneratedRegex(@"<a[^>]*?href=""[^""]*?where-i-live\?uprn=(?<uprn>[^&""]+)(?:&amp;a=(?<property>[^&""]+))?[^""]*""[^>]*>(?<label>[^<]+)<\/a>")]
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
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
				clientSideResponse.Headers["set-cookie"]
			);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"https://www.wakefield.gov.uk/pick-your-address?where-i-live={postcode}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "cookie", requestCookies },
				},
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

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var uprn = HttpUtility.UrlDecode(rawAddress.Groups["uprn"].Value).Trim();

				string? property;
				if (rawAddress.Groups["property"].Success)
				{
					property = HttpUtility.UrlDecode(rawAddress.Groups["property"].Value).Trim();
				}
				else
				{
					property = rawAddress.Groups["label"].Value.Trim();
				}

				var address = new Address
				{
					Property = property,
					Postcode = postcode,
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
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://www.wakefield.gov.uk/where-i-live?uprn={address.Uid}&a={Uri.EscapeDataString(address.Property!)}",
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

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["service"].Value.Trim();
				var nextCollection = rawBinDay.Groups["next"].Value.Trim();
				var futureCollections = rawBinDay.Groups["future"].Value.Trim();

				var matchingBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);
				var collectionDates = new HashSet<DateOnly>();

				if (!nextCollection.Contains("n/a", StringComparison.OrdinalIgnoreCase))
				{
					var date = DateOnly.ParseExact(
						nextCollection,
						"dddd, d MMMM yyyy",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);
					collectionDates.Add(date);
				}

				foreach (Match dateMatch in DateRegex().Matches(futureCollections))
				{
					var date = DateOnly.ParseExact(
						dateMatch.Groups["date"].Value,
						"dddd, d MMMM yyyy",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);
					collectionDates.Add(date);
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
