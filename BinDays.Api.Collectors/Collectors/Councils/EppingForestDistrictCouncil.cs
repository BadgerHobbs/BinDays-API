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
/// Collector implementation for Epping Forest District Council.
/// </summary>
internal sealed partial class EppingForestDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Epping Forest District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.eppingforestdc.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "epping-forest";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Refuse Service" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling Service" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "Food Waste Service" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Garden Waste Service" ],
		},
	];

	/// <summary>
	/// Base URL for the Epping Forest Achieve Service endpoints.
	/// </summary>
	private const string _baseUrl = "https://eppingforestdc-self.achieveservice.com";

	/// <summary>
	/// The URL for the Epping Forest waste collection service page, used to obtain session cookies.
	/// </summary>
	private const string _serviceUrl = $"{_baseUrl}/en/AchieveForms/?mode=fill&consentMessage=yes&form_uri=sandbox-publish://AF-Process-4375316a-d4f9-4f86-aefc-48b69a35b908/AF-Stage-ba3fbec7-ab51-488c-8cad-4fb45a53643a/definition.json&process=1&process_uri=sandbox-processes://AF-Process-4375316a-d4f9-4f86-aefc-48b69a35b908&process_id=AF-Process-4375316a-d4f9-4f86-aefc-48b69a35b908&noLoginPrompt=1";

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
						"search": { "value": "{{postcode}}" }
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/apibroker/runLookup?id=603e231fa2367&sid={sessionId}",
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
				? rowsData.EnumerateObject().Select(property => property.Value)
				: rowsData.EnumerateArray();

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var addressEntry in addressEntries)
			{
				var address = new Address
				{
					Property = addressEntry.GetProperty("display").GetString()!.Trim(),
					Postcode = postcode,
					Uid = addressEntry.GetProperty("uprn").GetString()!,
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
					"Section 1": {
						"LookupUPRN": { "value": "{{address.Uid}}" }
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/apibroker/runLookup?id=6651dfb99a74d&sid={sessionId}",
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
			var row = jsonDocument.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("rows_data")
				.GetProperty("0");

			// Garden waste is a chargeable subscription; its fields are only present in the
			// response for addresses with an active subscription. Every other collection type
			// is always present, so those fail fast via GetProperty if unexpectedly missing.
			var collectionTypes = new[]
			{
				("GeneralWasteServiceName", "GeneralWasteServiceNextCollection", isOptional: false),
				("RecyclingServiceName", "RecyclingServiceNextCollection", isOptional: false),
				("FoodWasteServiceName", "FoodWasteServiceNextCollection", isOptional: false),
				("GardenWasteServiceName", "GardenWasteServiceNextCollection", isOptional: true),
			};

			// Iterate through each collection type, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var (nameField, nextCollectionField, isOptional) in collectionTypes)
			{
				string service;
				if (isOptional)
				{
					if (!row.TryGetProperty(nameField, out var serviceElement))
					{
						continue;
					}

					service = serviceElement.GetString()!;
				}
				else
				{
					service = row.GetProperty(nameField).GetString()!;
				}

				var collectionDate = row.GetProperty(nextCollectionField).GetString()!;

				if (string.IsNullOrWhiteSpace(collectionDate))
				{
					continue;
				}

				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				binDays.Add(new BinDay
				{
					Date = DateUtilities.ParseDateExact(collectionDate, "yyyy-MM-ddTHH:mm:ss"),
					Address = address,
					Bins = matchedBins,
				});
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
