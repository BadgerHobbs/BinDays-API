namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Crawley Borough Council.
/// </summary>
internal sealed partial class CrawleyBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Crawley Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://crawley.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "crawley";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes = [
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Rubbish" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Red,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Garden Waste" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "Food Waste" ],
		},
	];

	/// <summary>
	/// The base URL for myCrawley API requests.
	/// </summary>
	private const string _baseUrl = "https://my.crawley.gov.uk";

	/// <summary>
	/// Regex to extract the session identifier (sid) from HTML.
	/// </summary>
	[GeneratedRegex(@"sid=(?<sessionId>[a-f0-9]+)")]
	private static partial Regex SessionIdRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting session cookies
		if (clientSideResponse == null)
		{
			var clientSideRequest = CreateSessionRequest(1);

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for address lookup
		else if (clientSideResponse.RequestId == 1)
		{
			var (sessionId, cookies) = ExtractSessionData(clientSideResponse);

			var requestBody = $$"""
			{
				"formValues": {
					"Address": {
						"Is_the_address": {
							"value": "inBorough"
						},
						"PostcodeSearch": {
							"value": "{{postcode}}"
						},
						"aValidation": {
							"value": "false"
						}
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/apibroker/runLookup?id=5ae1ea87c883f&sid={sessionId}",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.ApplicationJson },
					{ "cookie", cookies },
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
			var rowsData = GetRowsData(jsonDoc);

			if (rowsData.ValueKind == JsonValueKind.Array)
			{
				var emptyAddressesResponse = new GetAddressesResponse
				{
					Addresses = [],
				};

				return emptyAddressesResponse;
			}

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var property in rowsData.EnumerateObject())
			{
				var addressData = property.Value;
				var uprn = addressData.GetProperty("uprn").GetString()!.Trim();
				var usrn = addressData.GetProperty("usrn").GetString()!.Trim();

				var address = new Address
				{
					Property = addressData.GetProperty("display").GetString()!.Trim(),
					Postcode = postcode,
					// Uid format: "uprn;usrn"
					Uid = $"{uprn};{usrn}",
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
		// Uid format: "uprn;usrn"
		var uidParts = address.Uid!.Split(';', 2);
		var uprn = uidParts[0];
		var usrn = uidParts[1];

		// Prepare client-side request for getting session cookies
		if (clientSideResponse == null)
		{
			var clientSideRequest = CreateSessionRequest(1);

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for bin day lookup
		else if (clientSideResponse.RequestId == 1)
		{
			var (sessionId, cookies) = ExtractSessionData(clientSideResponse);
			var currentDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/London")).ToString("dd/MM/yyyy");

			var requestBody = $$"""
			{
				"formValues": {
					"Address": {
						"address": {
							"value": {
								"Address": {
									"usrn": {
										"value": "{{usrn}}"
									},
									"uprn": {
										"value": "{{uprn}}"
									}
								}
							}
						},
						"dayConverted": {
							"value": "{{currentDate}}"
						},
						"getCollection": {
							"value": "true"
						},
						"getWorksheets": {
							"value": "false"
						}
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/apibroker/?api=RunLookup&id=5b4f0ec5f13f4&sid={sessionId}",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.ApplicationJson },
					{ "cookie", cookies },
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
			var rowsData = GetRowsData(jsonDoc);

			if (rowsData.ValueKind == JsonValueKind.Array)
			{
				return new GetBinDaysResponse { BinDays = [] };
			}

			// Iterate through each bin day row, and create new bin day objects
			var binDays = new List<BinDay>();
			foreach (var row in rowsData.EnumerateObject())
			{
				var rowData = row.Value;

				AddBinDay(binDays, rowData.GetProperty("rubbishDateCurrent").GetString()!, "Rubbish", address);
				AddBinDay(binDays, rowData.GetProperty("rubbishDateNext").GetString()!, "Rubbish", address);
				AddBinDay(binDays, rowData.GetProperty("recycleDateCurrent").GetString()!, "Recycling", address);
				AddBinDay(binDays, rowData.GetProperty("recycleDateNext").GetString()!, "Recycling", address);
				AddBinDay(binDays, rowData.GetProperty("greenDateCurrent").GetString()!, "Garden Waste", address);
				AddBinDay(binDays, rowData.GetProperty("greenDateNext").GetString()!, "Garden Waste", address);
				AddBinDay(binDays, rowData.GetProperty("foodDateCurrent").GetString()!, "Food Waste", address);
				AddBinDay(binDays, rowData.GetProperty("foodDateNext").GetString()!, "Food Waste", address);
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
	/// Creates the initial session request.
	/// </summary>
	/// <param name="requestId">The request ID.</param>
	/// <returns>The configured client-side request.</returns>
	private static ClientSideRequest CreateSessionRequest(int requestId)
	{
		var clientSideRequest = new ClientSideRequest
		{
			RequestId = requestId,
			Url = $"{_baseUrl}/en/service/check_my_bin_collection?accept=yes&consentMessageIds[]=85",
			Method = "GET",
		};

		return clientSideRequest;
	}

	/// <summary>
	/// Extracts the session identifier and cookies from the response.
	/// </summary>
	/// <param name="clientSideResponse">The client-side response.</param>
	/// <returns>The session identifier and cookies.</returns>
	private static (string SessionId, string Cookies) ExtractSessionData(ClientSideResponse clientSideResponse)
	{
		var sessionId = SessionIdRegex().Match(clientSideResponse.Content).Groups["sessionId"].Value;
		var setCookieHeader = clientSideResponse.Headers["set-cookie"];
		var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

		return (sessionId, cookies);
	}

	/// <summary>
	/// Gets the rows_data element from an Achieve API response.
	/// </summary>
	/// <param name="jsonDoc">The parsed JSON document.</param>
	/// <returns>The rows_data element.</returns>
	private static JsonElement GetRowsData(JsonDocument jsonDoc)
	{
		var rowsData = jsonDoc.RootElement
			.GetProperty("integration")
			.GetProperty("transformed")
			.GetProperty("rows_data");

		return rowsData;
	}

	/// <summary>
	/// Adds a bin day when a collection date is present.
	/// </summary>
	/// <param name="binDays">The bin day list to append to.</param>
	/// <param name="dateString">The collection date string.</param>
	/// <param name="service">The service key used to match bin types.</param>
	/// <param name="address">The selected address.</param>
	private void AddBinDay(List<BinDay> binDays, string dateString, string service, Address address)
	{
		if (string.IsNullOrWhiteSpace(dateString))
		{
			return;
		}

		var bins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

		var binDay = new BinDay
		{
			Date = DateUtilities.ParseDateInferringYear(dateString.Trim(), "dddd d MMMM"),
			Address = address,
			Bins = bins,
		};

		binDays.Add(binDay);
	}
}
