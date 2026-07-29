namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for East Suffolk Council.
/// </summary>
internal sealed partial class EastSuffolkCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "East Suffolk Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.eastsuffolk.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "east-suffolk";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "RESIDUAL" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "FOOD" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "PLASTIC/METAL/CARTONS/GLASS" ],
		},
		new()
		{
			Name = "Paper and Cardboard Recycling",
			Colour = BinColour.Green,
			Keys = [ "PAPER/CARD" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "GARDEN" ],
		},
	];

	/// <summary>
	/// The base URL for East Suffolk's self-service website.
	/// </summary>
	private const string _baseUrl = "https://my.eastsuffolk.gov.uk";

	/// <summary>
	/// The URL for the East Suffolk bin collection finder page.
	/// </summary>
	private const string _serviceUrl = "https://my.eastsuffolk.gov.uk/service/Bin_collection_dates_finder";

	/// <summary>
	/// Regex to extract the session identifier from the HTML response.
	/// </summary>
	[GeneratedRegex(@"sid=(?<sessionId>[a-zA-Z0-9]+)")]
	private static partial Regex SessionIdRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for starting the session
		if (clientSideResponse == null)
		{
			return new GetAddressesResponse
			{
				NextClientSideRequest = CreateInitialSessionRequest(),
			};
		}
		// Prepare client-side request for getting addresses
		else if (clientSideResponse.RequestId == 1)
		{
			var (requestCookies, sessionId) = GetSessionDetails(clientSideResponse);

			var requestBody = $$"""
			{
				"formValues": {
					"Section 1": {
						"alt_postcode_search": {
							"value": "{{postcode}}"
						}
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/apibroker/runLookup?id=60647bd7e6892&sid={sessionId}",
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

			// 'rows_data' is an empty array rather than an object when there are no results.
			var addressEntries = rowsData.ValueKind == JsonValueKind.Object
				? rowsData.EnumerateObject().Select(p => p.Value)
				: rowsData.EnumerateArray();

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rowData in addressEntries)
			{
				var address = new Address
				{
					Property = rowData.GetProperty("display").GetString()!.Trim(),
					Postcode = postcode,
					Uid = rowData.GetProperty("uprn").GetString()!,
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
			return new GetBinDaysResponse
			{
				NextClientSideRequest = CreateInitialSessionRequest(),
			};
		}
		// Prepare client-side request for getting an authentication token
		else if (clientSideResponse.RequestId == 1)
		{
			var (requestCookies, sessionId) = GetSessionDetails(clientSideResponse);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/apibroker/runLookup?id=59e73f8bd860c&sid={sessionId}",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.ApplicationJson },
					{ "cookie", requestCookies },
				},
				Body = "{}",
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", requestCookies },
						{ "sessionId", sessionId },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting bin days
		else if (clientSideResponse.RequestId == 2)
		{
			using var authJsonDocument = JsonDocument.Parse(clientSideResponse.Content);
			var authRowsData = authJsonDocument.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("rows_data");

			var authenticateResponse = authRowsData.GetProperty("0").GetProperty("AuthenticateResponse").GetString()!;
			var requestCookies = clientSideResponse.Options.Metadata["cookie"];
			var sessionId = clientSideResponse.Options.Metadata["sessionId"];
			var minimumDate = DateTime.UtcNow.Date.ToString("yyyy-MM-ddT00:00:00");
			var maximumDate = DateTime.UtcNow.Date.AddDays(28).ToString("yyyy-MM-ddT00:00:00");

			var requestBody = $$"""
			{
				"formValues": {
					"Details": {
						"AuthenticateResponse": {
							"value": "{{authenticateResponse}}"
						},
						"finalUPRN": {
							"value": "{{address.Uid!}}"
						},
						"minimum_date": {
							"value": "{{minimumDate}}"
						},
						"maximum_date": {
							"value": "{{maximumDate}}"
						}
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"{_baseUrl}/apibroker/runLookup?id=68f900a32e7a4&sid={sessionId}",
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
		else if (clientSideResponse.RequestId == 3)
		{
			using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);
			var rowsData = jsonDocument.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("rows_data");

			// 'rows_data' is an empty array rather than an object when there are no results.
			var collectionEntries = rowsData.ValueKind == JsonValueKind.Object
				? rowsData.EnumerateObject().Select(p => p.Value)
				: rowsData.EnumerateArray();

			// Iterate through each collection entry, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var rowData in collectionEntries)
			{
				var service = rowData.GetProperty("CollectionType").GetString()!.Trim();
				var collectionDate = rowData.GetProperty("CollectionDate").GetString()!.Trim();

				if (string.IsNullOrWhiteSpace(collectionDate))
				{
					continue;
				}

				var bins = ProcessingUtilities.GetMatchingBins(_binTypes, service);
				var binDay = new BinDay
				{
					Date = DateUtilities.ParseDateExact(collectionDate, "yyyy-MM-ddTHH:mm:ss"),
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

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <summary>
	/// Creates the initial GET request to start a session with the self-service portal.
	/// </summary>
	/// <returns>A <see cref="ClientSideRequest"/> for the initial session request.</returns>
	private static ClientSideRequest CreateInitialSessionRequest() => new()
	{
		RequestId = 1,
		Url = _serviceUrl,
		Method = "GET",
	};

	/// <summary>
	/// Extracts the request cookies and session identifier from a session response.
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
