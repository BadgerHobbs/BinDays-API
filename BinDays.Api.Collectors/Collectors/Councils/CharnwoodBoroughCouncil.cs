namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Charnwood Borough Council.
/// </summary>
internal sealed partial class CharnwoodBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Charnwood Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.charnwood.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "charnwood";

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
			Name = "Mixed Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden Waste" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Food" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// The base URL of the council's My Charnwood site.
	/// </summary>
	private const string _baseUrl = "https://my.charnwood.gov.uk";

	/// <summary>
	/// Regex for the addresses from the property finder results.
	/// </summary>
	[GeneratedRegex(@"location\?put=(?<uid>[a-z0-9]+)[^>]*>(?<property>[^<]+)</a><p class=""address_locality"">(?<locality>[^<]+)</p>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the link to the next page of property finder results.
	/// </summary>
	[GeneratedRegex(@"rel=""next""")]
	private static partial Regex NextPageRegex();

	/// <summary>
	/// Regex for the bin days from the data.
	/// </summary>
	[GeneratedRegex(@"<strong class=""date"">(?<date>[^<]+)</strong><a[^>]*>(?<service>[^<]+)</a>")]
	private static partial Regex BinDaysRegex();

	/// <summary>
	/// Regex for removing the ordinal suffix from a date (e.g. "15th" -> "15").
	/// </summary>
	[GeneratedRegex(@"(?<=\d)(st|nd|rd|th)")]
	private static partial Regex OrdinalRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = CreateAddressesRequest(postcode, 1, string.Empty),
			};

			return getAddressesResponse;
		}
		// Process addresses from response, following pagination as required
		else if (clientSideResponse.RequestId == 1)
		{
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

			// Restore addresses accumulated from previous pages
			var addresses = new List<Address>();
			foreach (var accumulatedAddress in clientSideResponse.Options.Metadata["addresses"].Split('\n', StringSplitOptions.RemoveEmptyEntries))
			{
				var parts = accumulatedAddress.Split('|', 2);

				var address = new Address
				{
					Property = parts[1],
					Postcode = postcode,
					Uid = parts[0],
				};

				addresses.Add(address);
			}

			// Iterate through each address, and create a new address object
			foreach (Match rawAddress in rawAddresses)
			{
				var address = new Address
				{
					Property = $"{rawAddress.Groups["property"].Value.Trim()}, {rawAddress.Groups["locality"].Value.Trim()}",
					Postcode = postcode,
					Uid = rawAddress.Groups["uid"].Value,
				};

				addresses.Add(address);
			}

			// Request the next page of results if there is one
			if (NextPageRegex().IsMatch(clientSideResponse.Content))
			{
				var page = int.Parse(clientSideResponse.Options.Metadata["page"], CultureInfo.InvariantCulture);

				// Iterate through each address, and serialise it to carry forward to the next page
				var serialisedAddresses = new List<string>();
				foreach (var address in addresses)
				{
					serialisedAddresses.Add($"{address.Uid}|{address.Property}");
				}

				var nextPageResponse = new GetAddressesResponse
				{
					NextClientSideRequest = CreateAddressesRequest(postcode, page + 1, string.Join('\n', serialisedAddresses)),
				};

				return nextPageResponse;
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
		// Prepare client-side request for setting the location, which returns the location cookie
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/location?put={address.Uid}",
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
			var cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/",
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
				var service = rawBinDay.Groups["service"].Value;

				// Dates are relative or have no year (e.g. "Today" or "Thu 15th Oct")
				var rawDate = OrdinalRegex().Replace(rawBinDay.Groups["date"].Value, string.Empty);
				var date = DateUtilities.ParseRelativeDateOrInferYear(rawDate, "ddd d MMM");

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

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

	/// <summary>
	/// Creates the client-side request for a page of property finder results, carrying forward the
	/// addresses from previous pages (one per line, as "uid|property").
	/// </summary>
	private static ClientSideRequest CreateAddressesRequest(string postcode, int page, string addresses)
	{
		var clientSideRequest = new ClientSideRequest
		{
			RequestId = 1,
			Url = $"{_baseUrl}/my-property-finder?address={postcode}&page={page}",
			Method = "GET",
			Options = new ClientSideOptions
			{
				Metadata =
				{
					{ "page", page.ToString(CultureInfo.InvariantCulture) },
					{ "addresses", addresses },
				},
			},
		};

		return clientSideRequest;
	}
}
