namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Sheffield City Council.
/// </summary>
internal sealed partial class SheffieldCityCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Sheffield City Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://sheffield.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "sheffield";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "Non-Recyclable Waste",
			Colour = BinColour.Black,
			Keys = [ "Black Bin" ],
		},
		new()
		{
			Name = "Paper and Card Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Blue Bin" ],
		},
		new()
		{
			Name = "Glass, Tins and Plastic Bottles Recycling",
			Colour = BinColour.Brown,
			Keys = [ "Brown Bin" ],
		},
	];

	/// <summary>
	/// The base URL for the Sheffield waste services portal.
	/// </summary>
	private const string _baseUrl = "https://wasteservices.sheffield.gov.uk";

	/// <summary>
	/// Regex for parsing addresses from the search response.
	/// </summary>
	[GeneratedRegex("""<option value="/property/(?<uid>[^"]+)">(?<address>[^<]+)</option>""")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for parsing bin collections from the property page.
	/// </summary>
	[GeneratedRegex(
		"""<tr class="service-id-[^"]+"[^>]*>\s*<td class="service-name">.*?<h4>(?<service>[^<]+)</h4>.*?<td class="next-service">\s*<span class="table-label">[^<]+<\/span>\s*(?<dates>[^<]+)\s*<\/td>""",
		RegexOptions.Singleline
	)]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting session cookie
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/",
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
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/property/",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", "application/x-www-form-urlencoded; charset=UTF-8" },
					{ "x-requested-with", "XMLHttpRequest" },
					{ "cookie", requestCookies },
				},
				Body = $"aj=true&search_property={postcode}",
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
			using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);

			var resultContent = jsonDocument.RootElement.GetProperty("result").GetString()!;
			var rawAddresses = AddressRegex().Matches(resultContent)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var uid = rawAddress.Groups["uid"].Value.Trim();
				var property = rawAddress.Groups["address"].Value.Trim();

				var address = new Address
				{
					Property = property,
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
		// Prepare client-side request for getting session cookie
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/",
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
		// Prepare client-side request for fetching bin day data
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/property/{address.Uid}",
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
				var service = rawBinDay.Groups["service"].Value.Trim();
				var dateStrings = rawBinDay.Groups["dates"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries);

				// Iterate through each collection date for the service
				foreach (var dateString in dateStrings)
				{
					var trimmedDate = dateString.Trim();

					var date = DateOnly.ParseExact(
						trimmedDate,
						"d MMM yyyy",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);

					var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

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
