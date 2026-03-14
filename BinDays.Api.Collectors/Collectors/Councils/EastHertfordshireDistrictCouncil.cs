namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for East Hertfordshire District Council.
/// </summary>
internal sealed partial class EastHertfordshireDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "East Hertfordshire District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.eastherts.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "east-hertfordshire";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Black,
			Keys = [ "Mixed Recycling (Black)" ],
		},
		new()
		{
			Name = "Cardboard and Paper",
			Colour = BinColour.Blue,
			Keys = [ "Cardboard and Paper (Blue)" ],
		},
		new()
		{
			Name = "Refuse",
			Colour = BinColour.Purple,
			Keys = [ "Refuse (Purple)" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "Food Waste (Caddy)" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden" ],
		},
	];

	/// <summary>
	/// Base URL for the East Hertfordshire Achieve Service endpoints.
	/// </summary>
	private const string _baseUrl = "https://eastherts-self.achieveservice.com";

	/// <summary>
	/// Regex to extract the session identifier (sid) from the HTML response.
	/// </summary>
	[GeneratedRegex(@"sid=(?<sessionId>[a-f0-9]+)")]
	private static partial Regex SessionIdRegex();

	/// <summary>
	/// Regex to remove ordinal suffixes from date strings.
	/// </summary>
	[GeneratedRegex(@"(?<=\d)(st|nd|rd|th)")]
	private static partial Regex OrdinalSuffixRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting session cookies and session ID
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/service/Bins___When_are_my_Bin_Collection_days",
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for getting addresses using the session details
		else if (clientSideResponse.RequestId == 1)
		{
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);
			var sessionId = SessionIdRegex().Match(clientSideResponse.Content).Groups["sessionId"].Value;

			var requestBody = $$"""
{
	"formValues": {
		"Collection Days": {
			"postcode_search": {
				"value": "{{postcode}}"
			}
		}
	}
}
""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/apibroker/runLookup?id=ffd7cff10d464&sid={sessionId}",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.ApplicationJson },
					{ "cookie", requestCookies },
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
		else if (clientSideResponse.RequestId == 2)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rows = jsonDoc.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("rows_data");

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var row in rows.EnumerateObject())
			{
				var addressData = row.Value;

				var address = new Address
				{
					Property = addressData.GetProperty("display").GetString()!.Trim(),
					Postcode = postcode,
					Uid = addressData.GetProperty("uprn").GetString()!,
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
		// Prepare client-side request for getting session cookies and session ID
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/service/Bins___When_are_my_Bin_Collection_days",
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting bin days using the session details
		else if (clientSideResponse.RequestId == 1)
		{
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);
			var sessionId = SessionIdRegex().Match(clientSideResponse.Content).Groups["sessionId"].Value;

			var requestBody = $$"""
{
	"formValues": {
		"Collection Days": {
			"postcode_search": {
				"value": "{{address.Postcode}}"
			},
			"listSelectAddress": {
				"value": "{{address.Uid}}"
			},
			"addressUPRN": {
				"value": "{{address.Uid}}"
			},
			"inputUPRN": {
				"value": "{{address.Uid}}"
			},
			"timeCheck": {
				"value": "continue"
			}
		}
	}
}
""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/apibroker/runLookup?id=683d9ff0e299d&sid={sessionId}",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.ApplicationJson },
					{ "cookie", requestCookies },
				},
				Body = requestBody,
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
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rows = jsonDoc.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("rows_data");

			JsonElement collectionData = default;
			foreach (var row in rows.EnumerateObject())
			{
				collectionData = row.Value;
				break;
			}

			var collections = new (string Service, string Date)[]
			{
				(collectionData.GetProperty("RecyclingServiceName").GetString()!, collectionData.GetProperty("RecyclingNextDate").GetString()!),
				(collectionData.GetProperty("PaperServiceName").GetString()!, collectionData.GetProperty("PaperNextDate").GetString()!),
				(collectionData.GetProperty("GWServiceName").GetString()!, collectionData.GetProperty("GWNextDate").GetString()!),
				(collectionData.GetProperty("FoodServiceName").GetString()!, collectionData.GetProperty("FoodNextDate").GetString()!),
				(collectionData.GetProperty("RefuseServiceName").GetString()!, collectionData.GetProperty("RefuseNextDate").GetString()!),
			};

			// Iterate through each bin collection, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var (service, collectionDate) in collections)
			{
				if (string.IsNullOrWhiteSpace(collectionDate))
				{
					continue;
				}

				var cleanedDate = OrdinalSuffixRegex().Replace(collectionDate.Trim(), string.Empty);
				var date = DateUtilities.ParseDateInferringYear(
					cleanedDate.Trim(),
					"dddd d MMMM"
				);

				var bins = ProcessingUtilities.GetMatchingBins(_binTypes, service.Trim());

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = bins,
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
