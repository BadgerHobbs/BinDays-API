namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
			Keys = [ "grey", "240d", "domestic", "grey wheelie" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Green,
			Keys = [ "green", "240g", "recycling", "green wheelie" ],
		},
	];

	private const string _baseUrl = "https://my.kirklees.gov.uk";
	private const string _servicePath = "/service/Bins_and_recycling___Manage_your_bins";
	private const string _apiBrokerPath = "/apibroker/runLookup";
	private const string _addressLookupId = "58049013ca4c9";
	private const string _propertyDetailsLookupId = "659c2c2386104";
	private const string _binListLookupId = "65e08e60b299d";
	private const string _scheduleLookupId = "692431ec1ec18";
	private const int _dateRangeDays = 28;

	/// <summary>
	/// Regex to extract the session ID (sid) from HTML content.
	/// </summary>
	[GeneratedRegex(@"sid=([a-f0-9]+)")]
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

			var sid = SidRegex().Match(clientSideResponse.Content).Groups[1].Value;

			var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

			var requestBody = $$"""
			{
				"formValues": {
					"Section 1": {
						"searchForAddress": {
							"name": "searchForAddress",
							"value": "yes",
							"isMandatory": true,
							"type": "radio"
						},
						"Postcode": {
							"name": "Postcode",
							"value": "{{postcode}}",
							"isMandatory": true
						}
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}{_apiBrokerPath}?id={_addressLookupId}&repeat_against=&noRetry=false&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&_={timestamp}&sid={sid}",
				Method = "POST",
				Headers = new()
				{
					{"content-type", "application/json"},
					{"x-requested-with", "XMLHttpRequest"},
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

				var address = new Address
				{
					Property = rowData.GetProperty("display").GetString()!.Trim(),
					Street = rowData.GetProperty("Street").GetString()!.Trim(),
					Town = rowData.GetProperty("Town").GetString()!.Trim(),
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

			var sid = SidRegex().Match(clientSideResponse.Content).Groups[1].Value;

			var house = address.Property!.Split(",").FirstOrDefault()?.Split(" ").FirstOrDefault() ?? string.Empty;

			var requestBody = $$"""
			{
				"formValues": {
					"Search": {
						"PowerSuite_Available": { "name": "PowerSuite_Available", "value": "True", "isMandatory": true },
						"PowerSuite_Available1": { "name": "PowerSuite_Available1", "value": "True", "isMandatory": true },
						"customerAddress": {
							"Section 1": {
								"searchForAddress": { "name": "searchForAddress", "value": "yes", "isMandatory": true, "type": "radio" },
								"Postcode": { "name": "Postcode", "value": "{{address.Postcode!}}", "isMandatory": true },
								"List": { "name": "List", "value": "{{address.Uid!}}", "isMandatory": true, "type": "select", "value_label": "{{address.Property}}" },
								"House": { "name": "House", "value": "{{house}}", "isMandatory": true },
								"Street": { "name": "Street", "value": "{{address.Street!}}", "isMandatory": true },
								"Town": { "name": "Town", "value": "{{address.Town!}}", "isMandatory": true },
								"UPRN": { "name": "UPRN", "value": "{{address.Uid!}}", "isMandatory": true },
								"fullAddress": { "name": "fullAddress", "value": "{{address.Property!}}", "isMandatory": true }
							}
						},
						"uprn2": { "name": "uprn2", "value": "{{address.Uid!}}", "isMandatory": true },
						"validatedUPRN": { "name": "validatedUPRN", "value": "{{address.Uid!}}", "isMandatory": true },
						"suppliedUPRN": { "name": "suppliedUPRN", "value": "{{address.Uid!}}", "isMandatory": true },
						"productName": { "name": "productName", "value": "Self", "isMandatory": true },
						"uprnFinal": { "name": "uprnFinal", "value": "{{address.Uid!}}", "isMandatory": true },
						"houseFinal": { "name": "houseFinal", "value": "{{house}}", "isMandatory": true },
						"streetFinal": { "name": "streetFinal", "value": "{{address.Street!}}", "isMandatory": true },
						"townFinal": { "name": "townFinal", "value": "{{address.Town!}}", "isMandatory": true },
						"postcodeFinal": { "name": "postcodeFinal", "value": "{{address.Postcode!}}", "isMandatory": true },
						"fullAddressFinal": { "name": "fullAddressFinal", "value": "{{address.Property!}}", "isMandatory": true },
						"binsPropertyType": {
							"Section 1": {
								"PropertyType": { "name": "PropertyType", "value": "Residential", "isMandatory": true }
							}
						},
						"validPropertyFlag": { "name": "validPropertyFlag", "value": "yes", "isMandatory": true }
					}
				}
			}
			""";

			var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			var fromDate = DateTime.UtcNow.AddDays(-_dateRangeDays).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
			var toDate = DateTime.UtcNow.AddDays(_dateRangeDays).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}{_apiBrokerPath}?id={_propertyDetailsLookupId}&repeat_against=&noRetry=false&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&_={timestamp}&sid={sid}",
				Method = "POST",
				Headers = new()
				{
					{"content-type", "application/json"},
					{"x-requested-with", "XMLHttpRequest"},
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
			using var responseJson = JsonDocument.Parse(clientSideResponse.Content);
			var rowsData = responseJson.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("rows_data");

			var govDeliveryCategory = rowsData.EnumerateObject().First().Value
				.GetProperty("GovDeliveryCategorye")
				.GetString()!
				.Trim();

			var sid = clientSideResponse.Options.Metadata["sid"];
			var cookies = clientSideResponse.Options.Metadata["cookies"];
			var fromDate = clientSideResponse.Options.Metadata["fromDate"];
			var toDate = clientSideResponse.Options.Metadata["toDate"];

			var house = address.Property!.Split(",").FirstOrDefault()?.Split(" ").FirstOrDefault() ?? string.Empty;
			var currentDateTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
			var timeInHours = DateTime.UtcNow.ToString("HH", CultureInfo.InvariantCulture);

			var requestBody = $$"""
			{
				"formValues": {
					"Search": {
						"PowerSuite_Available": { "name": "PowerSuite_Available", "value": "True", "isMandatory": true },
						"PowerSuite_Available1": { "name": "PowerSuite_Available1", "value": "True", "isMandatory": true },
						"customerAddress": {
							"Section 1": {
								"searchForAddress": { "name": "searchForAddress", "value": "yes", "isMandatory": true, "type": "radio" },
								"Postcode": { "name": "Postcode", "value": "{{address.Postcode!}}", "isMandatory": true },
								"List": { "name": "List", "value": "{{address.Uid!}}", "isMandatory": true, "type": "select", "value_label": "{{address.Property}}" },
								"House": { "name": "House", "value": "{{house}}", "isMandatory": true },
								"Street": { "name": "Street", "value": "{{address.Street!}}", "isMandatory": true },
								"Town": { "name": "Town", "value": "{{address.Town!}}", "isMandatory": true },
								"UPRN": { "name": "UPRN", "value": "{{address.Uid!}}", "isMandatory": true },
								"fullAddress": { "name": "fullAddress", "value": "{{address.Property!}}", "isMandatory": true }
							}
						},
						"uprn2": { "name": "uprn2", "value": "{{address.Uid!}}", "isMandatory": true },
						"validatedUPRN": { "name": "validatedUPRN", "value": "{{address.Uid!}}", "isMandatory": true },
						"suppliedUPRN": { "name": "suppliedUPRN", "value": "{{address.Uid!}}", "isMandatory": true },
						"productName": { "name": "productName", "value": "Self", "isMandatory": true },
						"uprnFinal": { "name": "uprnFinal", "value": "{{address.Uid!}}", "isMandatory": true },
						"houseFinal": { "name": "houseFinal", "value": "{{house}}", "isMandatory": true },
						"streetFinal": { "name": "streetFinal", "value": "{{address.Street!}}", "isMandatory": true },
						"townFinal": { "name": "townFinal", "value": "{{address.Town!}}", "isMandatory": true },
						"postcodeFinal": { "name": "postcodeFinal", "value": "{{address.Postcode!}}", "isMandatory": true },
						"fullAddressFinal": { "name": "fullAddressFinal", "value": "{{address.Property!}}", "isMandatory": true },
						"binsPropertyType": {
							"Section 1": {
								"PropertyType": { "name": "PropertyType", "value": "Residential", "isMandatory": true },
								"GovDeliveryCategorye": { "name": "GovDeliveryCategorye", "value": "{{govDeliveryCategory}}", "isMandatory": true }
							}
						},
						"validPropertyFlag": { "name": "validPropertyFlag", "value": "yes", "isMandatory": true }
					},
					"Your bins": {
						"NextCollectionFromDate": { "name": "NextCollectionFromDate", "value": "{{fromDate}}", "isMandatory": true },
						"NextCollectionToDate": { "name": "NextCollectionToDate", "value": "{{toDate}}", "isMandatory": true },
						"currentDateTime": { "name": "currentDateTime", "value": "{{currentDateTime}}", "isMandatory": true },
						"timeInHours": { "name": "timeInHours", "value": "{{timeInHours}}", "isMandatory": true },
						"sameDaySubmissionFlag": { "name": "sameDaySubmissionFlag", "value": "no", "isMandatory": true },
						"maxBinAllocation": { "name": "maxBinAllocation", "value": "1", "isMandatory": true },
						"allowedBins": { "name": "allowedBins", "value": "1", "isMandatory": true }
					}
				}
			}
			""";

			var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"{_baseUrl}{_apiBrokerPath}?id={_binListLookupId}&repeat_against=&noRetry=false&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&_={timestamp}&sid={sid}",
				Method = "POST",
				Headers = new()
				{
					{"content-type", "application/json"},
					{"x-requested-with", "XMLHttpRequest"},
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
						{ "govDeliveryCategory", govDeliveryCategory },
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
				var binTypeService = source.Contains("240G", StringComparison.OrdinalIgnoreCase)
					|| source.Contains("green", StringComparison.OrdinalIgnoreCase)
					? "Recycling Collection Service"
					: "Domestic Waste Collection Service";

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
				clientSideResponse.Options.Metadata["govDeliveryCategory"],
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

				var date = DateOnly.ParseExact(
					collectionData,
					"dddd d MMMM yyyy",
					CultureInfo.InvariantCulture,
					DateTimeStyles.None
				);

				binDays.Add(new BinDayData(date, currentBin.Label));
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
					clientSideResponse.Options.Metadata["govDeliveryCategory"],
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
		string govDeliveryCategory,
		string fromDate,
		string toDate,
		string binData,
		int requestId,
		ClientSideOptions options
	)
	{
		var house = address.Property!.Split(",").FirstOrDefault()?.Split(" ").FirstOrDefault() ?? string.Empty;
		var binTypeSelect = bin.BinTypeService.Contains("Recycling", StringComparison.OrdinalIgnoreCase) ? "Recycling" : "Domestic";
		var currentDateTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
		var timeInHours = DateTime.UtcNow.ToString("HH", CultureInfo.InvariantCulture);

		var requestBody = $$"""
		{
			"formValues": {
				"Search": {
					"PowerSuite_Available": { "name": "PowerSuite_Available", "value": "True", "isMandatory": true },
					"PowerSuite_Available1": { "name": "PowerSuite_Available1", "value": "True", "isMandatory": true },
					"customerAddress": {
						"Section 1": {
							"searchForAddress": { "name": "searchForAddress", "value": "yes", "isMandatory": true, "type": "radio" },
							"Postcode": { "name": "Postcode", "value": "{{address.Postcode!}}", "isMandatory": true },
							"List": { "name": "List", "value": "{{address.Uid!}}", "isMandatory": true, "type": "select", "value_label": "{{address.Property}}" },
							"House": { "name": "House", "value": "{{house}}", "isMandatory": true },
							"Street": { "name": "Street", "value": "{{address.Street!}}", "isMandatory": true },
							"Town": { "name": "Town", "value": "{{address.Town!}}", "isMandatory": true },
							"UPRN": { "name": "UPRN", "value": "{{address.Uid!}}", "isMandatory": true },
							"fullAddress": { "name": "fullAddress", "value": "{{address.Property!}}", "isMandatory": true }
						}
					},
					"uprn2": { "name": "uprn2", "value": "{{address.Uid!}}", "isMandatory": true },
					"validatedUPRN": { "name": "validatedUPRN", "value": "{{address.Uid!}}", "isMandatory": true },
					"suppliedUPRN": { "name": "suppliedUPRN", "value": "{{address.Uid!}}", "isMandatory": true },
					"productName": { "name": "productName", "value": "Self", "isMandatory": true },
					"uprnFinal": { "name": "uprnFinal", "value": "{{address.Uid!}}", "isMandatory": true },
					"houseFinal": { "name": "houseFinal", "value": "{{house}}", "isMandatory": true },
					"streetFinal": { "name": "streetFinal", "value": "{{address.Street!}}", "isMandatory": true },
					"townFinal": { "name": "townFinal", "value": "{{address.Town!}}", "isMandatory": true },
					"postcodeFinal": { "name": "postcodeFinal", "value": "{{address.Postcode!}}", "isMandatory": true },
					"fullAddressFinal": { "name": "fullAddressFinal", "value": "{{address.Property!}}", "isMandatory": true },
					"binsPropertyType": {
						"Section 1": {
							"PropertyType": { "name": "PropertyType", "value": "Residential", "isMandatory": true },
							"GovDeliveryCategorye": { "name": "GovDeliveryCategorye", "value": "{{govDeliveryCategory}}", "isMandatory": true }
						}
					},
					"validPropertyFlag": { "name": "validPropertyFlag", "value": "yes", "isMandatory": true }
				},
				"Your bins": {
					"binTypeService": { "name": "binTypeService", "value": "{{bin.BinTypeService}}", "isMandatory": true },
					"RoundSchedule": { "name": "RoundSchedule", "value": "{{bin.RoundSchedule}}", "isMandatory": true },
					"NextCollectionFromDate": { "name": "NextCollectionFromDate", "value": "{{fromDate}}", "isMandatory": true },
					"NextCollectionToDate": { "name": "NextCollectionToDate", "value": "{{toDate}}", "isMandatory": true },
					"binData": { "name": "binData", "value": "{{binData}}", "isMandatory": true },
					"serviceItemID": { "name": "serviceItemID", "value": "{{bin.ServiceItemId}}", "isMandatory": true },
					"binTypeSelect": { "name": "binTypeSelect", "value": "{{binTypeSelect}}", "isMandatory": true },
					"currentDateTime": { "name": "currentDateTime", "value": "{{currentDateTime}}", "isMandatory": true },
					"timeInHours": { "name": "timeInHours", "value": "{{timeInHours}}", "isMandatory": true },
					"sameDaySubmissionFlag": { "name": "sameDaySubmissionFlag", "value": "no", "isMandatory": true },
					"maxBinAllocation": { "name": "maxBinAllocation", "value": "1", "isMandatory": true },
					"allowedBins": { "name": "allowedBins", "value": "1", "isMandatory": true }
				}
			}
		}
		""";

		var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

		var clientSideRequest = new ClientSideRequest
		{
			RequestId = requestId,
			Url = $"{_baseUrl}{_apiBrokerPath}?id={_scheduleLookupId}&repeat_against=&noRetry=false&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&_={timestamp}&sid={sid}",
			Method = "POST",
			Headers = new()
			{
				{"content-type", "application/json"},
				{"x-requested-with", "XMLHttpRequest"},
				{"cookie", cookies},
				{"user-agent", Constants.UserAgent},
			},
			Body = requestBody,
			Options = options,
		};

		return clientSideRequest;
	}

	private sealed record BinInfo(string Label, string RoundSchedule, string BinTypeService, string ServiceItemId);

	private sealed record BinDayData(DateOnly Date, string BinLabel);
}
