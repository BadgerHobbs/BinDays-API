namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Tendring District Council.
/// </summary>
internal sealed partial class TendringDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Tendring District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.tendringdc.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "tendring";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Residual" ],
		},
		new()
		{
			Name = "Metals & Plastics Recycling",
			Colour = BinColour.Green,
			Keys = [ "Green" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Paper & Card Recycling",
			Colour = BinColour.Red,
			Keys = [ "Red" ],
			Type = BinType.Box,
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
	/// The URL for the Tendring waste collection service page, used to obtain session cookies.
	/// </summary>
	private const string _serviceUrl = "https://tendring-self.achieveservice.com/en/service/Rubbish_and_recycling_collection_days";

	/// <summary>
	/// Regex to extract the session identifier from the service page response.
	/// </summary>
	[GeneratedRegex(@"sid=(?<sessionId>[a-zA-Z0-9]+)")]
	private static partial Regex SessionIdRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for starting the session
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _serviceUrl,
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for getting addresses
		else if (clientSideResponse.RequestId == 1)
		{
			var (requestCookies, sessionId) = GetSessionDetails(clientSideResponse);

			var requestBody = $$"""
			{
				"formValues": {
					"Section 1": {
						"postcode_search": { "value": "{{postcode}}" }
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"https://tendring-self.achieveservice.com/apibroker/runLookup?id=5bacce91c0d86&sid={sessionId}",
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
			using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);
			var rowsData = jsonDocument.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("rows_data");

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var row in rowsData.EnumerateObject())
			{
				var address = new Address
				{
					Property = row.Value.GetProperty("display").GetString()!.Trim(),
					Postcode = postcode,
					Uid = row.Value.GetProperty("uprn").GetString()!,
				};

				addresses.Add(address);
			}

			var getAddressesResponse = new GetAddressesResponse
			{
				Addresses = [.. addresses],
			};

			return getAddressesResponse;
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for starting the session
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _serviceUrl,
				Method = "GET",
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
			var (requestCookies, sessionId) = GetSessionDetails(clientSideResponse);

			var requestBody = $$"""
			{
				"formValues": {
					"Select address": {
						"selectedUPRN": { "value": "{{address.Uid}}" }
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"https://tendring-self.achieveservice.com/apibroker/runLookup?id=6347acbadc425&sid={sessionId}",
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
			using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);
			var rowsData = jsonDocument.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("rows_data");

			var row = rowsData.GetProperty("0");
			var binDays = new List<BinDay>();
			var collectionTypes = new[]
			{
				("RefuseNextCol", "RefusePreviousCol", "Residual"),
				("DMRNextCol", "DMRPreviousCol", "Green"),
				("PaperNextCol", "PaperPreviousCol", "Red"),
				("FoodNextCol", "FoodPreviousCol", "Food"),
			};

			// Iterate through each collection type and its next/previous dates
			foreach (var (nextField, prevField, binKey) in collectionTypes)
			{
				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, binKey);
				var dates = new[] { row.GetProperty(nextField).GetString()!, row.GetProperty(prevField).GetString()! };

				foreach (var dateString in dates)
				{
					if (string.IsNullOrWhiteSpace(dateString))
					{
						continue;
					}

					binDays.Add(new BinDay
					{
						Date = DateUtilities.ParseDateExact(dateString, "dd/MM/yyyy HH:mm:ss"),
						Address = address,
						Bins = matchedBins,
					});
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

	/// <summary>
	/// Extracts the request cookies and session identifier from the service page response.
	/// </summary>
	/// <param name="clientSideResponse">The client-side response containing cookies and session content.</param>
	/// <returns>A tuple containing the request cookies and session identifier.</returns>
	private static (string RequestCookies, string SessionId) GetSessionDetails(ClientSideResponse clientSideResponse)
	{
		var setCookieHeader = clientSideResponse.Headers["set-cookie"];
		var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);
		var sessionId = SessionIdRegex().Match(clientSideResponse.Content).Groups["sessionId"].Value;

		return (requestCookies, sessionId);
	}
}
