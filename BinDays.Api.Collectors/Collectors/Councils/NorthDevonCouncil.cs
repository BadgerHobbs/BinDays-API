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
/// Collector implementation for North Devon Council.
/// </summary>
internal sealed partial class NorthDevonCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "North Devon Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.northdevon.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "north-devon";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Waste-Black" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Green,
			Type = BinType.Container,
			Keys = [ "Waste-Recycling" ],
		},
	];

	/// <summary>
	/// Regex to match individual collection entries in work packs.
	/// </summary>
	[GeneratedRegex(@"(?<label>.+)/(?<date>\d{2}/\d{2}/\d{4})")]
	private static partial Regex CollectionDateRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Handle initial session setup (steps 1-2)
		var (sessionRequest, shouldContinue) = HandleSessionInitialization(clientSideResponse);
		if (!shouldContinue)
		{
			return new GetAddressesResponse
			{
				NextClientSideRequest = sessionRequest,
			};
		}

		// Prepare client-side request for getting location data
		if (clientSideResponse!.RequestId == 2)
		{
			var clientSideRequest = CreateLocationRequest(clientSideResponse, postcode);

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for looking up addresses
		else if (clientSideResponse.RequestId == 3)
		{
			var cookies = AccumulateCookies(clientSideResponse);
			var prevMetadata = clientSideResponse.Options.Metadata;
			var metadata = new Dictionary<string, string>
			{
				{ "cookies", cookies },
				{ "sid", prevMetadata["sid"] },
				{ "postcode", prevMetadata["postcode"] },
			};

			return new GetAddressesResponse
			{
				NextClientSideRequest = CreateAddressLookupRequest(4, "5849617f4ce25", metadata),
			};
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 4)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rowsData = GetRowsData(jsonDoc);

			if (rowsData.ValueKind == JsonValueKind.Array)
			{
				return new GetAddressesResponse { Addresses = [] };
			}

			var addresses = new List<Address>();

			// Iterate through each address, and create a new address object
			foreach (var property in rowsData.EnumerateObject())
			{
				var data = property.Value;

				var address = new Address
				{
					Property = data.GetProperty("display").GetString()!.Trim(),
					Street = data.GetProperty("street").GetString()!.Trim(),
					Town = data.GetProperty("posttown").GetString()!.Trim(),
					Postcode = postcode,
					Uid = data.GetProperty("uprn").GetString()!.Trim(),
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
		// Handle initial session setup (steps 1-2)
		// Note: Session initialization is required for each GetBinDays call as the API
		// requires fresh session cookies and SID for the multi-step bin collection lookup process
		var (sessionRequest, shouldContinue) = HandleSessionInitialization(clientSideResponse);
		if (!shouldContinue)
		{
			return new GetBinDaysResponse
			{
				NextClientSideRequest = sessionRequest,
			};
		}

		// Prepare client-side request for getting location data
		if (clientSideResponse!.RequestId == 2)
		{
			var clientSideRequest = CreateLocationRequest(clientSideResponse, address.Postcode!);

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting address details
		else if (clientSideResponse.RequestId == 3)
		{
			var cookies = AccumulateCookies(clientSideResponse);
			var prevMetadata = clientSideResponse.Options.Metadata;
			var metadata = new Dictionary<string, string>
			{
				{ "cookies", cookies },
				{ "sid", prevMetadata["sid"] },
				{ "postcode", prevMetadata["postcode"] },
			};

			return new GetBinDaysResponse
			{
				NextClientSideRequest = CreateAddressLookupRequest(4, "65141c7c38bd0", metadata, address.Uid),
			};
		}
		// Prepare client-side request for getting live token
		else if (clientSideResponse.RequestId == 4)
		{
			return new GetBinDaysResponse
			{
				NextClientSideRequest = CreateAddressLookupRequest(5, "59e606ee95b7a", clientSideResponse.Options.Metadata, address.Uid),
			};
		}
		// Prepare client-side request for getting full address details
		else if (clientSideResponse.RequestId == 5)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var liveToken = GetRowsData(jsonDoc)
				.GetProperty("0")
				.GetProperty("liveToken")
				.GetString()!
				.Trim();

			var metadata = new Dictionary<string, string>(clientSideResponse.Options.Metadata)
			{
				{ "liveToken", liveToken },
			};

			return new GetBinDaysResponse
			{
				NextClientSideRequest = CreateAddressLookupRequest(6, "625587f465a91", metadata, address.Uid),
			};
		}
		// Prepare client-side request for getting calendar window
		else if (clientSideResponse.RequestId == 6)
		{
			return new GetBinDaysResponse
			{
				NextClientSideRequest = CreateAddressLookupRequest(7, "6255925ca44cb", clientSideResponse.Options.Metadata, address.Uid),
			};
		}
		// Prepare client-side request for getting collection results
		else if (clientSideResponse.RequestId == 7)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rows = GetRowsData(jsonDoc).GetProperty("0");

			var calendarStartDate = rows.GetProperty("calstartDate").GetString()!.Trim();
			var calendarEndDate = rows.GetProperty("calendDate").GetString()!.Trim();

			var metadata = new Dictionary<string, string>(clientSideResponse.Options.Metadata)
			{
				{ "calendarStartDate", calendarStartDate },
				{ "calendarEndDate", calendarEndDate },
			};

			var clientSideRequest = CreateCalendarLookupRequest(8, metadata, address);

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 8)
		{
			var getBinDaysResponse = ProcessBinDaysFromResponse(clientSideResponse, address);

			// If no bin days found, retry the request
			if (getBinDaysResponse.BinDays?.Count == 0)
			{
				var metadata = clientSideResponse.Options.Metadata;
				var clientSideRequest = CreateCalendarLookupRequest(9, metadata, address);

				return new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest,
				};
			}

			return getBinDaysResponse;
		}
		// Process bin days from retry response (used when initial request returns no data)
		else if (clientSideResponse.RequestId == 9)
		{
			return ProcessBinDaysFromResponse(clientSideResponse, address);
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <summary>
	/// Creates the location API request after session authentication (RequestId 2 → 3).
	/// </summary>
	private static ClientSideRequest CreateLocationRequest(ClientSideResponse clientSideResponse, string postcode)
	{
		var cookies = AccumulateCookies(clientSideResponse);

		using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
		var sid = GetSid(clientSideResponse, jsonDoc.RootElement);

		return new ClientSideRequest
		{
			RequestId = 3,
			Url = $"https://my.northdevon.gov.uk/apibroker/location?sid={sid}",
			Method = "GET",
			Headers = new()
			{
				{ "cookie", cookies },
				{ "x-requested-with", "XMLHttpRequest" },
				{ "User-Agent", Constants.UserAgent },
			},
			Options = new ClientSideOptions
			{
				Metadata = new Dictionary<string, string>
				{
					{ "cookies", cookies },
					{ "sid", sid },
					{ "postcode", postcode },
				},
			},
		};
	}

	/// <summary>
	/// Creates a calendar lookup request with the specified parameters.
	/// </summary>
	private static ClientSideRequest CreateCalendarLookupRequest(
		int requestId,
		Dictionary<string, string> metadata,
		Address address)
	{
		var cookies = metadata["cookies"];
		var sid = metadata["sid"];
		var postcode = metadata["postcode"];
		var liveToken = metadata["liveToken"];
		var calendarStartDate = metadata["calendarStartDate"];
		var calendarEndDate = metadata["calendarEndDate"];

		var requestBody = BuildRequestBody(new()
		{
			["Your address"] = CreateYourAddressFormValues(postcode, address.Uid),
			["Calendar"] = CreateCalendarFormValues(address.Uid!, liveToken, calendarStartDate, calendarEndDate),
		});

		return new ClientSideRequest
		{
			RequestId = requestId,
			Url = $"https://my.northdevon.gov.uk/apibroker/runLookup?id=61091d927cd81&sid={sid}",
			Method = "POST",
			Body = requestBody,
			Headers = new()
			{
				{ "content-type", "application/json" },
				{ "cookie", cookies },
				{ "x-requested-with", "XMLHttpRequest" },
				{ "User-Agent", Constants.UserAgent },
			},
			Options = new ClientSideOptions { Metadata = new Dictionary<string, string>(metadata) },
		};
	}

	/// <summary>
	/// Creates a standard address lookup POST request to the runLookup API.
	/// </summary>
	private static ClientSideRequest CreateAddressLookupRequest(
		int requestId, string lookupId, Dictionary<string, string> metadata, string? uid = null)
	{
		var requestBody = BuildRequestBody(new()
		{
			["Your address"] = CreateYourAddressFormValues(metadata["postcode"], uid),
		});

		return new ClientSideRequest
		{
			RequestId = requestId,
			Url = $"https://my.northdevon.gov.uk/apibroker/runLookup?id={lookupId}&sid={metadata["sid"]}",
			Method = "POST",
			Body = requestBody,
			Headers = new()
			{
				{ "content-type", "application/json" },
				{ "cookie", metadata["cookies"] },
				{ "x-requested-with", "XMLHttpRequest" },
				{ "User-Agent", Constants.UserAgent },
			},
			Options = new ClientSideOptions { Metadata = new Dictionary<string, string>(metadata) },
		};
	}

	/// <summary>
	/// Serializes the form values into the standard API request body JSON format.
	/// </summary>
	private static string BuildRequestBody(Dictionary<string, object?> formValues) =>
		JsonSerializer.Serialize(new
		{
			formId = "AF-Form-a9a357e7-8b6d-416e-b974-04a2aa857e87",
			formValues,
			processId = "AF-Process-d615d6eb-6718-4e33-a2ff-18f1e5e58f8b",
		});

	/// <summary>
	/// Combines cookies from metadata with new set-cookie headers from the response.
	/// </summary>
	private static string AccumulateCookies(ClientSideResponse response)
	{
		var previousCookies = response.Options.Metadata["cookies"];
		var newCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(response.Headers["set-cookie"]);
		return string.Join("; ", new[] { previousCookies, newCookies }.Where(c => !string.IsNullOrWhiteSpace(c)));
	}

	/// <summary>
	/// Creates the "Your address" form values for address lookup requests.
	/// </summary>
	private static object CreateYourAddressFormValues(string postcode, string? uid = null) => new
	{
		postcode_search = new { value = postcode },
		UPRNMF = new { value = uid ?? string.Empty },
	};

	/// <summary>
	/// Creates the "Calendar" form values for bin collection calendar requests.
	/// </summary>
	private static Dictionary<string, object?> CreateCalendarFormValues(
		string uprn, string liveToken,
		string startDate, string endDate) => new()
	{
		{ "token", new { value = liveToken } },
		{ "uPRN", new { value = uprn } },
		{ "calstartDate", new { value = startDate } },
		{ "calendDate", new { value = endDate } },
		{ "UPRN", new { value = uprn } },
		{ "liveToken", new { value = liveToken } },
	};

	/// <summary>
	/// Navigates to the rows_data element in the standard API response structure.
	/// </summary>
	private static JsonElement GetRowsData(JsonDocument jsonDoc) =>
		jsonDoc.RootElement
			.GetProperty("integration")
			.GetProperty("transformed")
			.GetProperty("rows_data");

	/// <summary>
	/// Extracts the session ID from the response header or JSON body.
	/// </summary>
	private static string GetSid(ClientSideResponse response, JsonElement rootElement)
	{
		var sidHeader = response.Headers
			.FirstOrDefault(h => h.Key.Equals("x-auth-session", StringComparison.OrdinalIgnoreCase)).Value;

		if (!string.IsNullOrWhiteSpace(sidHeader))
		{
			return sidHeader;
		}

		return rootElement.GetProperty("auth-session").GetString()!;
	}

	/// <summary>
	/// Handles the initial session and authentication setup (RequestId 1-2).
	/// </summary>
	private static (ClientSideRequest? clientSideRequest, bool shouldContinue) HandleSessionInitialization(
		ClientSideResponse? clientSideResponse)
	{
		// Step 1: Get initial session cookies
		if (clientSideResponse == null)
		{
			return (new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://my.northdevon.gov.uk/service/WasteRecyclingCollectionCalendar",
				Method = "GET",
				Headers = new()
				{
					{ "User-Agent", Constants.UserAgent },
				},
			}, false);
		}

		// Step 2: Authenticate and get session ID
		if (clientSideResponse.RequestId == 1)
		{
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
				clientSideResponse.Headers["set-cookie"]
			);

			return (new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://my.northdevon.gov.uk/authapi/isauthenticated?uri=https%3A%2F%2Fmy.northdevon.gov.uk%2Fservice%2FWasteRecyclingCollectionCalendar&hostname=my.northdevon.gov.uk&withCredentials=true",
				Method = "GET",
				Headers = new()
				{
					{ "cookie", requestCookies },
					{ "x-requested-with", "XMLHttpRequest" },
					{ "User-Agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions
				{
					Metadata = new Dictionary<string, string>
					{
						{ "cookies", requestCookies },
					},
				},
			}, false);
		}

		// Step 3: Continue with specific method logic
		return (null, true);
	}

	/// <summary>
	/// Processes the bin collection API response into a collection of BinDay objects.
	/// </summary>
	/// <param name="clientSideResponse">The client-side response containing the JSON data.</param>
	/// <param name="address">The address for which bin days are being retrieved.</param>
	/// <returns>A GetBinDaysResponse containing the processed bin days.</returns>
	private GetBinDaysResponse ProcessBinDaysFromResponse(ClientSideResponse clientSideResponse, Address address)
	{
		using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
		var rowsData = GetRowsData(jsonDoc);

		var binDaysByDate = ParseBinDaysFromRowsData(rowsData);

		var binDays = binDaysByDate
			.Select(kvp => new BinDay
			{
				Date = kvp.Key,
				Address = address,
				Bins = [.. kvp.Value],
			})
			.ToList();

		return new GetBinDaysResponse
		{
			BinDays = ProcessingUtilities.ProcessBinDays(binDays),
		};
	}

	/// <summary>
	/// Parses bin collection days from the API response data.
	/// </summary>
	/// <param name="rowsData">The JSON element containing the rows data from the API response.</param>
	/// <returns>A dictionary mapping collection dates to sets of bins to be collected.</returns>
	private Dictionary<DateOnly, HashSet<Bin>> ParseBinDaysFromRowsData(JsonElement rowsData)
	{
		var binDaysByDate = new Dictionary<DateOnly, HashSet<Bin>>();

		if (rowsData.ValueKind == JsonValueKind.Object)
		{
			foreach (var row in rowsData.EnumerateObject())
			{
				var value = row.Value;
				if (!value.TryGetProperty("WorkPack", out var workPackElement) ||
					!value.TryGetProperty("ServiceDetail", out var serviceDetailElement))
				{
					continue;
				}

				var workPack = workPackElement.GetString()!.Trim();
				var serviceDetail = serviceDetailElement.GetString()!.Trim();

				var match = CollectionDateRegex().Match(serviceDetail);

				if (!match.Success)
				{
					continue;
				}

				var collectionDate = DateOnly.ParseExact(
					match.Groups["date"].Value,
					"dd/MM/yyyy",
					CultureInfo.InvariantCulture,
					DateTimeStyles.None
				);

				var binsForDate = binDaysByDate.GetValueOrDefault(collectionDate, []);
				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, workPack);

				if (matchedBins.Count == 0)
				{
					continue;
				}

				binsForDate.UnionWith(matchedBins);
				binDaysByDate[collectionDate] = binsForDate;
			}
		}

		return binDaysByDate;
	}
}
