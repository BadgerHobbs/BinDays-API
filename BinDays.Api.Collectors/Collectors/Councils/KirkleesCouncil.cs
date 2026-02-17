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
/// Collector implementation for Kirklees Council.
/// </summary>
internal sealed partial class KirkleesCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Kirklees Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.kirklees.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "kirklees";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Grey,
			Keys = [ "domestic" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Green,
			Keys = [ "recycling" ],
		},
	];

	/// <summary>
	/// The base URL for the Kirklees Council self-service portal.
	/// </summary>
	private const string _baseUrl = "https://my.kirklees.gov.uk";

	/// <summary>
	/// The service path for the bin collection management service.
	/// </summary>
	private const string _servicePath = "/service/Bins_and_recycling___Manage_your_bins";

	/// <summary>
	/// The number of days before and after today to fetch bin collections for.
	/// </summary>
	private const int _dateRangeDays = 28;

	/// <summary>
	/// Regex to extract the session ID (sid) from HTML content.
	/// </summary>
	[GeneratedRegex(@"sid=(?<sid>[a-f0-9]+)")]
	private static partial Regex SidRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}{_servicePath}",
				Method = "GET",
				Headers = new()
				{
					{"user-agent", Constants.UserAgent},
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookies = clientSideResponse.Headers["set-cookie"];
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookies);

			var sid = SidRegex().Match(clientSideResponse.Content).Groups["sid"].Value;

			var requestBody = $$"""
			{
				"formValues": {
					"Section 1": {
						"searchForAddress": {
							"value": "yes"
						},
						"Postcode": {
							"value": "{{postcode}}"
						}
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = BuildApiBrokerUrl("58049013ca4c9", sid),
				Method = "POST",
				Headers = new()
				{
					{"content-type", "application/json"},
					{"cookie", requestCookies},
					{"user-agent", Constants.UserAgent},
				},
				Body = requestBody,
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Map addresses from response
		else if (clientSideResponse.RequestId == 2)
		{
			using var responseJson = JsonDocument.Parse(clientSideResponse.Content);
			var rowsData = responseJson.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("rows_data");

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var row in rowsData.EnumerateObject())
			{
				var rowData = row.Value;
				var uid = rowData.GetProperty("name").GetString()!;

				var property = rowData.GetProperty("display").GetString()!.Trim();
				var street = rowData.GetProperty("Street").GetString()!.Trim();
				var town = rowData.GetProperty("Town").GetString()!.Trim();

				var address = new Address
				{
					Property = property,
					Street = street,
					Town = town,
					Postcode = postcode,
					Uid = $"{uid};{property};{street};{town}",
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
		// Uid format: "uprn;property;street;town"
		var uidParts = address.Uid!.Split(';', 4);
		address = new Address
		{
			Uid = uidParts[0],
			Property = uidParts[1],
			Street = uidParts[2],
			Town = uidParts[3],
			Postcode = address.Postcode,
		};

		// Prepare client-side request for getting bin days
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}{_servicePath}",
				Method = "GET",
				Headers = new()
				{
					{"user-agent", Constants.UserAgent},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare property details request
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookies = clientSideResponse.Headers["set-cookie"];
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookies);

			var sid = SidRegex().Match(clientSideResponse.Content).Groups["sid"].Value;

			var searchFormValues = BuildSearchFormValues(address);

			var requestBody = $$"""
			{
				"formValues": {
					"Search": {{searchFormValues}}
				}
			}
			""";

			var fromDate = DateTime.UtcNow.AddDays(-_dateRangeDays).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
			var toDate = DateTime.UtcNow.AddDays(_dateRangeDays).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = BuildApiBrokerUrl("659c2c2386104", sid),
				Method = "POST",
				Headers = new()
				{
					{"content-type", "application/json"},
					{"cookie", requestCookies},
					{"user-agent", Constants.UserAgent},
				},
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "sid", sid },
						{ "cookies", requestCookies },
						{ "fromDate", fromDate },
						{ "toDate", toDate },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process property details and request bin list
		else if (clientSideResponse.RequestId == 2)
		{
			var sid = clientSideResponse.Options.Metadata["sid"];
			var cookies = clientSideResponse.Options.Metadata["cookies"];
			var fromDate = clientSideResponse.Options.Metadata["fromDate"];
			var toDate = clientSideResponse.Options.Metadata["toDate"];

			var searchFormValues = BuildSearchFormValues(address);

			var requestBody = $$"""
			{
				"formValues": {
					"Search": {{searchFormValues}},
					"Your bins": {
						"NextCollectionFromDate": { "value": "{{fromDate}}" },
						"NextCollectionToDate": { "value": "{{toDate}}" }
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = BuildApiBrokerUrl("65e08e60b299d", sid),
				Method = "POST",
				Headers = new()
				{
					{"content-type", "application/json"},
					{"cookie", cookies},
					{"user-agent", Constants.UserAgent},
				},
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "sid", sid },
						{ "cookies", cookies },
						{ "fromDate", fromDate },
						{ "toDate", toDate },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin list and prepare schedule requests
		else if (clientSideResponse.RequestId == 3)
		{
			using var responseJson = JsonDocument.Parse(clientSideResponse.Content);
			var rowsData = responseJson.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("rows_data");

			var bins = new List<BinInfo>();
			var binDetails = new List<string>();
			var seenServiceItemIds = new HashSet<string>();

			// Iterate through each bin, and create a new bin info object
			foreach (var row in rowsData.EnumerateObject())
			{
				var rowData = row.Value;
				var serviceItemId = rowData.GetProperty("ServiceItemID").GetString()!;

				if (!seenServiceItemIds.Add(serviceItemId))
				{
					continue;
				}

				var label = rowData.GetProperty("label").GetString()!.Trim();
				var roundSchedule = rowData.GetProperty("RoundSchedule").GetString()!.Trim();
				var serviceItemName = rowData.GetProperty("ServiceItemName").GetString()!.Trim();

				var source = $"{serviceItemName} {label}";
				var isRecycling = source.Contains("240G", StringComparison.OrdinalIgnoreCase)
					|| source.Contains("green", StringComparison.OrdinalIgnoreCase);
				var binTypeService = isRecycling ? "Recycling Collection Service" : "Domestic Waste Collection Service";

				bins.Add(new BinInfo(label, roundSchedule, binTypeService, serviceItemId));

				binDetails.Add(rowData.GetProperty("BinDetails").GetString()!.Trim());
			}

			var binData = string.Join(",", binDetails);

			clientSideResponse.Options.Metadata.Add("binData", binData);
			clientSideResponse.Options.Metadata.Add("binIndex", "0");
			clientSideResponse.Options.Metadata.Add("bins", JsonSerializer.Serialize(bins));
			clientSideResponse.Options.Metadata.Add("binDays", "[]");

			var nextRequest = BuildScheduleRequest(
				address,
				bins[0],
				clientSideResponse.Options.Metadata["sid"],
				clientSideResponse.Options.Metadata["cookies"],
				clientSideResponse.Options.Metadata["fromDate"],
				clientSideResponse.Options.Metadata["toDate"],
				binData,
				4,
				clientSideResponse.Options
			);

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = nextRequest,
			};

			return getBinDaysResponse;
		}
		// Process schedule responses
		else if (clientSideResponse.RequestId == 4)
		{
			var bins = JsonSerializer.Deserialize<List<BinInfo>>(clientSideResponse.Options.Metadata["bins"]!)!;
			var binIndex = int.Parse(clientSideResponse.Options.Metadata["binIndex"], CultureInfo.InvariantCulture);
			var currentBin = bins[binIndex];

			using var responseJson = JsonDocument.Parse(clientSideResponse.Content);
			var rowsData = responseJson.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("rows_data");

			var binDays = JsonSerializer.Deserialize<List<BinDayData>>(clientSideResponse.Options.Metadata["binDays"]!)!;

			// Iterate through each collection date, and create a new bin day record
			foreach (var row in rowsData.EnumerateObject())
			{
				var collectionData = row.Value.GetProperty("Collections").GetString()!.Trim();

				var date = collectionData switch
				{
					var value when value.Equals("Today", StringComparison.OrdinalIgnoreCase) =>
						DateOnly.FromDateTime(DateTime.UtcNow),
					var value when value.Equals("Tomorrow", StringComparison.OrdinalIgnoreCase) =>
						DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
					_ => DateOnly.ParseExact(
						collectionData,
						"dddd d MMMM yyyy",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					),
				};

				binDays.Add(new BinDayData(date, currentBin.BinTypeService));
			}

			if (binIndex + 1 < bins.Count)
			{
				var nextIndex = binIndex + 1;
				clientSideResponse.Options.Metadata["binIndex"] = nextIndex.ToString(CultureInfo.InvariantCulture);
				clientSideResponse.Options.Metadata["binDays"] = JsonSerializer.Serialize(binDays);

				var nextRequest = BuildScheduleRequest(
					address,
					bins[nextIndex],
					clientSideResponse.Options.Metadata["sid"],
					clientSideResponse.Options.Metadata["cookies"],
					clientSideResponse.Options.Metadata["fromDate"],
					clientSideResponse.Options.Metadata["toDate"],
					clientSideResponse.Options.Metadata["binData"],
					4,
					clientSideResponse.Options
				);

				var nextBinDaysResponse = new GetBinDaysResponse
				{
					NextClientSideRequest = nextRequest,
				};

				return nextBinDaysResponse;
			}

			// Iterate through each bin day, and create a new bin day object
			var processedBinDays = new List<BinDay>();
			foreach (var binDay in binDays)
			{
				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, binDay.BinLabel);

				var processedBinDay = new BinDay
				{
					Date = binDay.Date,
					Address = address,
					Bins = matchedBins,
				};

				processedBinDays.Add(processedBinDay);
			}

			var getBinDaysResponse = new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(processedBinDays),
			};

			return getBinDaysResponse;
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <summary>
	/// Builds a schedule request for fetching bin collection dates.
	/// </summary>
	private static ClientSideRequest BuildScheduleRequest(
		Address address,
		BinInfo bin,
		string sid,
		string cookies,
		string fromDate,
		string toDate,
		string binData,
		int requestId,
		ClientSideOptions options
	)
	{
		var searchFormValues = BuildSearchFormValues(address);
		var requestBody = $$"""
		{
			"formValues": {
				"Search": {{searchFormValues}},
				"Your bins": {
					"binTypeService": { "value": "{{bin.BinTypeService}}" },
					"RoundSchedule": { "value": "{{bin.RoundSchedule}}" },
					"NextCollectionFromDate": { "value": "{{fromDate}}" },
					"NextCollectionToDate": { "value": "{{toDate}}" },
					"binData": { "value": "{{binData}}" },
					"serviceItemID": { "value": "{{bin.ServiceItemId}}" }
				}
			}
		}
		""";

		var clientSideRequest = new ClientSideRequest
		{
			RequestId = requestId,
			Url = BuildApiBrokerUrl("692431ec1ec18", sid),
			Method = "POST",
			Headers = new()
			{
				{"content-type", "application/json"},
				{"cookie", cookies},
				{"user-agent", Constants.UserAgent},
			},
			Body = requestBody,
			Options = options,
		};

		return clientSideRequest;
	}

	/// <summary>
	/// Builds the "Search" section of the form values JSON shared across multiple requests.
	/// </summary>
	private static string BuildSearchFormValues(Address address)
	{
		return $$"""
		{
			"customerAddress": {
				"Section 1": {
					"Postcode": { "value": "{{address.Postcode!}}" },
					"List": { "value": "{{address.Uid!}}" },
					"Street": { "value": "{{address.Street!}}" },
					"Town": { "value": "{{address.Town!}}" },
					"UPRN": { "value": "{{address.Uid!}}" }
				}
			},
			"uprn2": { "value": "{{address.Uid!}}" },
			"validatedUPRN": { "value": "{{address.Uid!}}" },
			"suppliedUPRN": { "value": "{{address.Uid!}}" }
		}
		""";
	}

	/// <summary>
	/// Builds the API broker URL for a given lookup ID and session.
	/// </summary>
	private static string BuildApiBrokerUrl(string lookupId, string sid)
	{
		var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		return $"{_baseUrl}/apibroker/runLookup?id={lookupId}&_={timestamp}&sid={sid}";
	}

	/// <summary>
	/// Holds bin metadata needed across schedule request steps.
	/// </summary>
	private sealed record BinInfo(string Label, string RoundSchedule, string BinTypeService, string ServiceItemId);

	/// <summary>
	/// Holds a parsed collection date and its associated bin label.
	/// </summary>
	private sealed record BinDayData(DateOnly Date, string BinLabel);
}
