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
				Url = "https://my.kirklees.gov.uk/service/Bins_and_recycling___Manage_your_bins",
				Method = "GET",
				Headers = new()
				{
					{"user-agent", Constants.UserAgent},
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookies = clientSideResponse.Headers["set-cookie"];
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookies);

			var sid = SidRegex().Match(clientSideResponse.Content).Groups[1].Value;

			var requestBodyObject = BuildAddressSearchBody(postcode);
			var requestBody = JsonSerializer.Serialize(requestBodyObject);

			var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"https://my.kirklees.gov.uk/apibroker/runLookup?id=58049013ca4c9&repeat_against=&noRetry=false&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&_={timestamp}&sid={sid}",
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
				NextClientSideRequest = clientSideRequest
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
				Url = "https://my.kirklees.gov.uk/service/Bins_and_recycling___Manage_your_bins",
				Method = "GET",
				Headers = new()
				{
					{"user-agent", Constants.UserAgent},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getBinDaysResponse;
		}
		// Prepare property details request
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookies = clientSideResponse.Headers["set-cookie"];
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookies);

			var sid = SidRegex().Match(clientSideResponse.Content).Groups[1].Value;

			var searchSection = BuildSearchSection(address, null);
			var requestBodyObject = new Dictionary<string, object>
			{
				{
					"formValues",
					new Dictionary<string, object>
					{
						{ "Search", searchSection },
					}
				},
			};

			var requestBody = JsonSerializer.Serialize(requestBodyObject);
			var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"https://my.kirklees.gov.uk/apibroker/runLookup?id=659c2c2386104&repeat_against=&noRetry=false&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&_={timestamp}&sid={sid}",
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
						{ "fromDate", DateTime.UtcNow.AddDays(-28).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) },
						{ "toDate", DateTime.UtcNow.AddDays(28).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest
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

			var searchSection = BuildSearchSection(address, govDeliveryCategory);

			var requestBodyObject = new Dictionary<string, object>
			{
				{
					"formValues",
					new Dictionary<string, object>
					{
						{ "Search", searchSection },
						{
							"Your bins",
							new Dictionary<string, object>
							{
								{ "NextCollectionFromDate", CreateField("NextCollectionFromDate", clientSideResponse.Options.Metadata["fromDate"], true) },
								{ "NextCollectionToDate", CreateField("NextCollectionToDate", clientSideResponse.Options.Metadata["toDate"], true) },
								{ "currentDateTime", CreateField("currentDateTime", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), true) },
								{ "timeInHours", CreateField("timeInHours", DateTime.UtcNow.ToString("HH", CultureInfo.InvariantCulture), true) },
								{ "sameDaySubmissionFlag", CreateField("sameDaySubmissionFlag", "no", true) },
								{ "maxBinAllocation", CreateField("maxBinAllocation", "1", true) },
								{ "allowedBins", CreateField("allowedBins", "1", true) },
							}
						},
					}
				},
			};

			var requestBody = JsonSerializer.Serialize(requestBodyObject);
			var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"https://my.kirklees.gov.uk/apibroker/runLookup?id=65e08e60b299d&repeat_against=&noRetry=false&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&_={timestamp}&sid={sid}",
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
						{ "fromDate", clientSideResponse.Options.Metadata["fromDate"] },
						{ "toDate", clientSideResponse.Options.Metadata["toDate"] },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest
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

			// Iterate through each bin, and create a new bin info object
			foreach (var row in rowsData.EnumerateObject())
				{
					var rowData = row.Value;
					var serviceItemId = rowData.GetProperty("ServiceItemID").GetString()!;

					if (bins.Any(x => x.ServiceItemId == serviceItemId))
				{
					continue;
				}

					var label = rowData.GetProperty("label").GetString()!.Trim();
					var roundSchedule = rowData.GetProperty("RoundSchedule").GetString()!.Trim();
					var serviceItemName = rowData.GetProperty("ServiceItemName").GetString()!.Trim();
					var binTypeService = GetBinTypeService(serviceItemName, label);

				bins.Add(new BinInfo(label, roundSchedule, binTypeService, serviceItemId));

				binDetails.Add(rowData.GetProperty("BinDetails").GetString()!.Trim());
			}

			var binData = string.Join(",", binDetails);

			var metadata = new ClientSideOptions
			{
				Metadata =
				{
					{ "sid", clientSideResponse.Options.Metadata["sid"] },
					{ "cookies", clientSideResponse.Options.Metadata["cookies"] },
					{ "govDeliveryCategory", clientSideResponse.Options.Metadata["govDeliveryCategory"] },
					{ "fromDate", clientSideResponse.Options.Metadata["fromDate"] },
					{ "toDate", clientSideResponse.Options.Metadata["toDate"] },
					{ "binData", binData },
					{ "binIndex", "0" },
					{ "bins", JsonSerializer.Serialize(bins) },
					{ "binDays", "[]" },
				},
			};

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
				metadata
			);

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = nextRequest
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
						NextClientSideRequest = nextRequest
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

	private static Dictionary<string, object> BuildAddressSearchBody(string postcode)
	{
		return new Dictionary<string, object>
		{
			{
				"formValues",
				new Dictionary<string, object>
				{
					{
						"Section 1",
						new Dictionary<string, object>
						{
							{ "searchForAddress", CreateField("searchForAddress", "yes", true, "radio") },
							{ "Postcode", CreateField("Postcode", postcode, true) },
							{ "postcode", CreateField("postcode", string.Empty, true) },
							{ "house", CreateField("house", string.Empty, true) },
							{ "flat", CreateField("flat", string.Empty, true) },
							{ "street", CreateField("street", string.Empty, true) },
							{ "town", CreateField("town", string.Empty, true) },
							{ "fullAddress", CreateField("fullAddress", string.Empty, true) },
						}
					},
				}
			},
		};
	}

	private static Dictionary<string, object> BuildSearchSection(Address address, string? govDeliveryCategory)
	{
		var house = address.Property?.Split(",").FirstOrDefault()?.Split(" ").FirstOrDefault() ?? string.Empty;

		return new Dictionary<string, object>
		{
			{ "PowerSuite_Available", CreateField("PowerSuite_Available", "True", true) },
			{ "PowerSuite_Available1", CreateField("PowerSuite_Available1", "True", true) },
			{
				"customerAddress",
				new Dictionary<string, object>
				{
					{
						"Section 1",
						new Dictionary<string, object>
						{
							{ "searchForAddress", CreateField("searchForAddress", "yes", true, "radio") },
							{ "Postcode", CreateField("Postcode", address.Postcode ?? string.Empty, true) },
							{ "List", CreateField("List", address.Uid ?? string.Empty, true, "select", address.Property) },
							{ "House", CreateField("House", house, true) },
							{ "Street", CreateField("Street", address.Street ?? string.Empty, true) },
							{ "Town", CreateField("Town", address.Town ?? string.Empty, true) },
							{ "UPRN", CreateField("UPRN", address.Uid ?? string.Empty, true) },
							{ "PropertyReference", CreateField("PropertyReference", string.Empty, true) },
							{ "postcode", CreateField("postcode", string.Empty, true) },
							{ "house", CreateField("house", string.Empty, true) },
							{ "flat", CreateField("flat", string.Empty, true) },
							{ "street", CreateField("street", string.Empty, true) },
							{ "town", CreateField("town", string.Empty, true) },
							{ "fullAddress", CreateField("fullAddress", address.Property ?? string.Empty, true) },
						}
					},
				}
			},
			{ "uprn2", CreateField("uprn2", address.Uid ?? string.Empty, true) },
			{ "validatedUPRN", CreateField("validatedUPRN", address.Uid ?? string.Empty, true) },
			{ "suppliedUPRN", CreateField("suppliedUPRN", address.Uid ?? string.Empty, true) },
			{ "productName", CreateField("productName", "Self", true) },
			{ "uprnFinal", CreateField("uprnFinal", address.Uid ?? string.Empty, true) },
			{ "houseFinal", CreateField("houseFinal", house, true) },
			{ "streetFinal", CreateField("streetFinal", address.Street ?? string.Empty, true) },
			{ "townFinal", CreateField("townFinal", address.Town ?? string.Empty, true) },
			{ "postcodeFinal", CreateField("postcodeFinal", address.Postcode ?? string.Empty, true) },
			{ "fullAddressFinal", CreateField("fullAddressFinal", address.Property ?? string.Empty, true) },
			{
				"binsPropertyType",
				new Dictionary<string, object>
				{
					{
						"Section 1",
						new Dictionary<string, object>
						{
							{ "PropertyType", CreateField("PropertyType", "Residential", true) },
							{ "GovDeliveryCategorye", CreateField("GovDeliveryCategorye", govDeliveryCategory ?? string.Empty, true) },
						}
					},
				}
			},
			{ "validPropertyFlag", CreateField("validPropertyFlag", "yes", true) },
			{ "refuse_Mesg1_Helper", CreateField("refuse_Mesg1_Helper", string.Empty, true) },
			{ "refuse_Mesg2_Helper", CreateField("refuse_Mesg2_Helper", string.Empty, true) },
		};
	}

	private static Dictionary<string, object> CreateField(string name, string value, bool isMandatory, string type = "text", string? valueLabel = null)
	{
		return new Dictionary<string, object>
		{
			{ "name", name },
			{ "type", type },
			{ "id", string.Empty },
			{ "value_changed", true },
			{ "section_id", string.Empty },
			{ "label", name },
			{ "value_label", valueLabel ?? string.Empty },
			{ "hasOther", false },
			{ "value", value },
			{ "path", string.Empty },
			{ "valid", true },
			{ "totals", string.Empty },
			{ "suffix", string.Empty },
			{ "prefix", string.Empty },
			{ "summary", string.Empty },
			{ "hidden", false },
			{ "_hidden", false },
			{ "isSummary", false },
			{ "staticMap", false },
			{ "isMandatory", isMandatory },
			{ "isRepeatable", false },
			{ "currencyPrefix", string.Empty },
			{ "decimalPlaces", string.Empty },
			{ "hash", string.Empty },
		};
	}

	private static string GetBinTypeService(string binType, string label)
	{
		var source = $"{binType} {label}";

		return source.Contains("240G", StringComparison.OrdinalIgnoreCase)
			|| source.Contains("green", StringComparison.OrdinalIgnoreCase)
			? "Recycling Collection Service"
			: "Domestic Waste Collection Service";
	}

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
		var searchSection = BuildSearchSection(address, govDeliveryCategory);

		var requestBodyObject = new Dictionary<string, object>
		{
			{
				"formValues",
				new Dictionary<string, object>
				{
					{ "Search", searchSection },
					{
						"Your bins",
						new Dictionary<string, object>
						{
							{ "binTypeService", CreateField("binTypeService", bin.BinTypeService, true) },
							{ "RoundSchedule", CreateField("RoundSchedule", bin.RoundSchedule, true) },
							{ "NextCollectionFromDate", CreateField("NextCollectionFromDate", fromDate, true) },
							{ "NextCollectionToDate", CreateField("NextCollectionToDate", toDate, true) },
							{ "binData", CreateField("binData", binData, true) },
							{ "serviceItemID", CreateField("serviceItemID", bin.ServiceItemId, true) },
							{ "binTypeSelect", CreateField("binTypeSelect", bin.BinTypeService.Contains("Recycling", StringComparison.OrdinalIgnoreCase) ? "Recycling" : "Domestic", true) },
							{ "currentDateTime", CreateField("currentDateTime", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), true) },
							{ "timeInHours", CreateField("timeInHours", DateTime.UtcNow.ToString("HH", CultureInfo.InvariantCulture), true) },
							{ "sameDaySubmissionFlag", CreateField("sameDaySubmissionFlag", "no", true) },
							{ "maxBinAllocation", CreateField("maxBinAllocation", "1", true) },
							{ "allowedBins", CreateField("allowedBins", "1", true) },
						}
					},
				}
			},
		};

		var requestBody = JsonSerializer.Serialize(requestBodyObject);
		var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

		var clientSideRequest = new ClientSideRequest
		{
			RequestId = requestId,
			Url = $"https://my.kirklees.gov.uk/apibroker/runLookup?id=692431ec1ec18&repeat_against=&noRetry=false&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&_={timestamp}&sid={sid}",
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
