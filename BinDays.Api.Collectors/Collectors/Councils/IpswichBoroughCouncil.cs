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
	/// Regex to extract addresses from the street search results list.
	/// </summary>
	[GeneratedRegex(@"<li>\s*<a href=""/bin-collection/weeks/(?<uid>\d+)"">(?<street>[^<]+)</a>\s*</li>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex to extract the street name and UID when the site skips address selection and returns
	/// bin days directly for an unambiguous street name.
	/// </summary>
	[GeneratedRegex(@"<h2>(?<street>[^<]+)</h2>[\s\S]*?/bin-collection/(?:pdf|months)/(?<uid>\d+)")]
	private static partial Regex DirectBinDaysRegex();

	/// <summary>
	/// Regex to extract each month's block from the "Collections by month" section.
	/// </summary>
	[GeneratedRegex(@"<h4>(?<monthYear>[^<]+)</h4>\s*<dl class=""ibc-columns ibc-zebra"">(?<body>[\s\S]*?)</dl>")]
	private static partial Regex MonthBlockRegex();

	/// <summary>
	/// Regex to extract a bin service name and its collection days from a month block.
	/// </summary>
	[GeneratedRegex(@"<dt>(?<service>[^<]+)</dt>\s*<dd>(?<days>[^<]+)</dd>")]
	private static partial Regex ServiceDaysRegex();

	/// <summary>
	/// Regex to extract a day-of-month number, ignoring its ordinal suffix (e.g. "6th" -> "6").
	/// </summary>
	[GeneratedRegex(@"\d+")]
	private static partial Regex DayNumberRegex();

	/// <summary>
	/// Regex to extract street names from the HTML-encoded autocomplete attribute. The &amp;quot;
	/// encoding only appears inside data-autocomplete, making the pattern unambiguous.
	/// </summary>
	[GeneratedRegex(@"&quot;(?<street>[^&]+)&quot;")]
	private static partial Regex AutocompleteStreetRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Step 1: GET postcodes.io to obtain lat/lon for the postcode
		if (clientSideResponse == null)
		{
			var clientSideRequest = GeocodingUtilities.CreatePostcodesIoRequest(postcode, 1);

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Step 2: GET Nominatim reverse geocode to resolve lat/lon to a road name
		else if (clientSideResponse.RequestId == 1)
		{
			var clientSideRequest = GeocodingUtilities.CreateNominatimReverseGeocodeRequest(clientSideResponse.Content, 2);

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Step 3: GET Ipswich search page to resolve the road name against the autocomplete list
		else if (clientSideResponse.RequestId == 2)
		{
			var road = GeocodingUtilities.ParseRoadName(clientSideResponse.Content)
				.Replace("'", string.Empty)
				.Replace("’", string.Empty);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"{_baseUrl}/bin-collection/",
				Method = "GET",
				Options = new ClientSideOptions
				{
					Metadata = { { "road", road } },
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Step 4: POST to Ipswich bin collection search using the normalized road name
		else if (clientSideResponse.RequestId == 3)
		{
			var road = clientSideResponse.Options.Metadata["road"];
			var normalizedRoad = road.Replace(" ", "").ToLowerInvariant();

			// Nominatim may use a different spelling (e.g. "Coleness Road" vs "Cole Ness Road");
			// the autocomplete list contains the canonical form, so find the normalized match.
			var streetName = road;
			foreach (Match m in AutocompleteStreetRegex().Matches(clientSideResponse.Content)!)
			{
				var street = m.Groups["street"].Value;
				if (street.Replace(" ", "").Equals(normalizedRoad, StringComparison.InvariantCultureIgnoreCase))
				{
					streetName = street;
					break;
				}
			}

			var clientSideRequest = BuildStreetSearchRequest(4, streetName);

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Parse addresses from Ipswich response
		else if (clientSideResponse.RequestId == 4)
		{
			var getAddressesResponse = ParseAddressesFromSearchResponse(clientSideResponse, postcode);

			// As with GetBinDays, this is an unproven candidate fix for occasional empty
			// responses, not a confirmed correction for a known cause (see project memory).
			if (getAddressesResponse.Addresses?.Count == 0)
			{
				var streetName = clientSideResponse.Options.Metadata["streetName"];
				var clientSideRequest = BuildStreetSearchRequest(5, streetName);

				return new GetAddressesResponse
				{
					NextClientSideRequest = clientSideRequest,
				};
			}

			return getAddressesResponse;
		}
		// Parse addresses from the retry response (used when the initial request returned no data)
		else if (clientSideResponse.RequestId == 5)
		{
			return ParseAddressesFromSearchResponse(clientSideResponse, postcode);
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <summary>
	/// Builds the client-side request that POSTs a street name to the Ipswich bin collection
	/// search, carrying the street name forward in metadata so a retry can reuse it.
	/// </summary>
	/// <param name="requestId">The request id to assign.</param>
	/// <param name="streetName">The normalized street name to search for.</param>
	/// <returns>The client-side request.</returns>
	private static ClientSideRequest BuildStreetSearchRequest(int requestId, string streetName)
	{
		var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
		{
			{ "street-name", streetName },
			{ "submit-button", string.Empty },
		});

		return new ClientSideRequest
		{
			RequestId = requestId,
			Url = $"{_baseUrl}/bin-collection/",
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", Constants.FormUrlEncoded },
			},
			Body = requestBody,
			Options = new ClientSideOptions
			{
				Metadata = { { "streetName", streetName } },
			},
		};
	}

	/// <summary>
	/// Parses addresses from an Ipswich bin collection street search response.
	/// </summary>
	/// <param name="clientSideResponse">The response containing the search results page.</param>
	/// <param name="postcode">The postcode the addresses belong to.</param>
	/// <returns>The response containing the parsed addresses.</returns>
	private static GetAddressesResponse ParseAddressesFromSearchResponse(ClientSideResponse clientSideResponse, string postcode)
	{
		var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

		if (rawAddresses.Count > 0)
		{
			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var street = WebUtility.HtmlDecode(rawAddress.Groups["street"].Value.Trim());
				var uid = rawAddress.Groups["uid"].Value.Trim();

				addresses.Add(new Address
				{
					Property = street,
					Postcode = postcode,
					Uid = uid,
				});
			}

			return new GetAddressesResponse
			{
				Addresses = [.. addresses],
			};
		}

		var directMatch = DirectBinDaysRegex().Match(clientSideResponse.Content);
		if (directMatch.Success)
		{
			var street = WebUtility.HtmlDecode(directMatch.Groups["street"].Value.Trim());
			var uid = directMatch.Groups["uid"].Value.Trim();

			return new GetAddressesResponse
			{
				Addresses =
				[
					new Address
					{
						Property = street,
						Postcode = postcode,
						Uid = uid,
					},
				],
			};
		}

		return new GetAddressesResponse { Addresses = [] };
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/bin-collection/months/{address.Uid!}",
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		else if (clientSideResponse.RequestId == 1)
		{
			var getBinDaysResponse = ParseBinDaysFromMonths(clientSideResponse, address);

			// The site occasionally returns a page with an empty schedule for reasons we haven't
			// confirmed (see project memory); a same-URL retry is an unproven candidate fix, not
			// a confirmed correction for a known cause.
			if (getBinDaysResponse.BinDays?.Count == 0)
			{
				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 2,
					Url = $"{_baseUrl}/bin-collection/months/{address.Uid!}",
					Method = "GET",
				};

				return new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest,
				};
			}

			return getBinDaysResponse;
		}
		// Process bin days from the retry response (used when the initial request returned no data)
		else if (clientSideResponse.RequestId == 2)
		{
			return ParseBinDaysFromMonths(clientSideResponse, address);
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <summary>
	/// Parses bin days from an Ipswich "Collections by month" page. Unlike the "Upcoming
	/// collections" page (which only lists the next 4 dates), this covers several months at
	/// once, so a single date failing to resolve doesn't zero out the whole response.
	/// </summary>
	/// <param name="clientSideResponse">The response containing the months page.</param>
	/// <param name="address">The address the bin days belong to.</param>
	/// <returns>The response containing the parsed bin days.</returns>
	private GetBinDaysResponse ParseBinDaysFromMonths(ClientSideResponse clientSideResponse, Address address)
	{
		var binDays = new List<BinDay>();

		foreach (Match monthBlock in MonthBlockRegex().Matches(clientSideResponse.Content)!)
		{
			var monthYear = monthBlock.Groups["monthYear"].Value.Trim();

			foreach (Match serviceDays in ServiceDaysRegex().Matches(monthBlock.Groups["body"].Value)!)
			{
				var service = WebUtility.HtmlDecode(serviceDays.Groups["service"].Value.Trim());
				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				if (matchedBins.Count == 0)
				{
					continue;
				}

				foreach (Match dayNumber in DayNumberRegex().Matches(serviceDays.Groups["days"].Value)!)
				{
					var date = DateUtilities.ParseDateExact(
						$"{dayNumber.Value} {monthYear}",
						"d MMMM yyyy"
					);

					binDays.Add(new BinDay
					{
						Date = date,
						Address = address,
						Bins = matchedBins,
					});
				}
			}
		}

		return new GetBinDaysResponse
		{
			BinDays = ProcessingUtilities.ProcessBinDays(binDays),
		};
	}
}
