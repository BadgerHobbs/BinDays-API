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
			Type = BinType.Bin,
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
	/// The identifier for the form used in the requests.
	/// </summary>
	private const string _formId = "AF-Form-a9a357e7-8b6d-416e-b974-04a2aa857e87";

	/// <summary>
	/// The identifier for the process used in the requests.
	/// </summary>
	private const string _processId = "AF-Process-d615d6eb-6718-4e33-a2ff-18f1e5e58f8b";

	/// <summary>
	/// The URL for the initial service request.
	/// </summary>
	private const string _serviceUrl = "https://my.northdevon.gov.uk/service/WasteRecyclingCollectionCalendar";

	/// <summary>
	/// The identifier for the stage used in the requests.
	/// </summary>
	private const string _stageId = "AF-Stage-0e576350-a6e1-444e-a105-cb020f910845";

	/// <summary>
	/// Regex to match individual collection entries in work packs.
	/// </summary>
	[GeneratedRegex(@"(?<label>.+)/(?<date>\d{2}/\d{2}/\d{4})")]
	private static partial Regex CollectionDateRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Handle initial session setup (steps 1-2)
		var (sessionRequest, shouldContinue) = HandleSessionInitialization(clientSideResponse, 3);
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
			var previousCookies = clientSideResponse.Options.Metadata.GetValueOrDefault("cookies", string.Empty);
			var newCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);
			var cookies = CombineCookies(previousCookies, newCookies);

			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var sid = GetSid(clientSideResponse, jsonDoc.RootElement);

			var formattedPostcode = ProcessingUtilities.FormatPostcode(postcode);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"https://my.northdevon.gov.uk/apibroker/location?app_name=AF-Renderer::Self&sid={sid}",
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
						{ "postcode", formattedPostcode },
					},
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for looking up addresses
		else if (clientSideResponse.RequestId == 3)
		{
			var previousCookies = clientSideResponse.Options.Metadata.GetValueOrDefault("cookies", string.Empty);
			var newCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);
			var cookies = CombineCookies(previousCookies, newCookies);

			var sid = clientSideResponse.Options.Metadata.GetValueOrDefault("sid", string.Empty);
			var formattedPostcode = clientSideResponse.Options.Metadata.GetValueOrDefault("postcode", string.Empty);

			// Note: Many fields below are empty but required by the API
			var requestBody = JsonSerializer.Serialize(new
			{
				stopOnFailure = true,
				usePHPIntegrations = true,
				stage_id = _stageId,
				stage_name = "Stage 1",
				formId = _formId,
				formValues = new Dictionary<string, object?>
				{
					["Your address"] = new
					{
						qsUPRN = new { value = string.Empty },
						postcode_search = new { value = formattedPostcode },
						chooseAddress = new { value = string.Empty },
						uprnfromlookup = new { value = string.Empty },
						UPRNMF = new { value = string.Empty },
						FULLADDR2 = new { value = string.Empty },
					},
					["Calendar"] = new Dictionary<string, object?>
					{
						{ "FULLADDR", new { value = string.Empty } },
						{ "token", new { value = string.Empty } },
						{ "uPRN", new { value = string.Empty } },
						{ "calstartDate", new { value = string.Empty } },
						{ "calendDate", new { value = string.Empty } },
						{ "details", Array.Empty<object>() },
						{ "text1", new { value = string.Empty } },
						{ "Results", new { value = string.Empty } },
						{ "UPRN", new { value = string.Empty } },
						{ "Alerts", new { value = string.Empty } },
						{ "liveToken", new { value = string.Empty } },
						{ "Results2", new { value = string.Empty } },
						{ "USRN", new { value = string.Empty } },
						{ "streetEvents", Array.Empty<object>() },
						{ "EventDescription", new { value = string.Empty } },
						{ "EventDate", new { value = string.Empty } },
						{ "EventsDisplay", new { value = string.Empty } },
						{ "Comments", new { value = string.Empty } },
						{ "OutText", new { value = string.Empty } },
						{ "StartDate", new { value = string.Empty } },
						{ "EndDate", new { value = string.Empty } },
					},
					["Print version"] = new
					{
						OutText2 = new { value = string.Empty },
					},
				},
				isPublished = true,
				formName = "WasteRecyclingCalendarForm",
				processId = _processId,
				tokens = new
				{
					site_url = _serviceUrl,
					site_path = "/service/WasteRecyclingCollectionCalendar",
					site_origin = "https://my.northdevon.gov.uk",
					product = "Self",
					formLanguage = "en",
					session_id = sid,
					formId = _formId,
					topFormId = _formId,
					parentFormId = _formId,
					formName = "WasteRecyclingCalendarForm",
					topFormName = "WasteRecyclingCalendarForm",
					parentFormName = "WasteRecyclingCalendarForm",
					processId = _processId,
					processName = "WasteRecyclingCollectionCalendar",
				},
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 4,
				Url = $"https://my.northdevon.gov.uk/apibroker/runLookup?id=5849617f4ce25&repeat_against=&noRetry=false&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&sid={sid}",
				Method = "POST",
				Body = requestBody,
				Headers = new()
				{
					{ "content-type", "application/json" },
					{ "cookie", cookies },
					{ "x-requested-with", "XMLHttpRequest" },
					{ "User-Agent", Constants.UserAgent },
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 4)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rowsData = jsonDoc.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("rows_data");

			var addresses = new List<Address>();

			if (rowsData.ValueKind == JsonValueKind.Array)
			{
				var getAddressesResponseEmpty = new GetAddressesResponse
				{
					Addresses = [.. addresses],
				};

				return getAddressesResponseEmpty;
			}

			// Iterate through each address, and create a new address object
			foreach (var property in rowsData.EnumerateObject())
			{
				var data = property.Value;

				var address = new Address
				{
					Property = data.GetProperty("display").GetString()!.Trim(),
					Street = data.GetProperty("street").GetString()!.Trim(),
					Town = data.GetProperty("posttown").GetString()!.Trim(),
					Postcode = ProcessingUtilities.FormatPostcode(postcode),
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
		var (sessionRequest, shouldContinue) = HandleSessionInitialization(clientSideResponse, 3);
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
			var previousCookies = clientSideResponse.Options.Metadata.GetValueOrDefault("cookies", string.Empty);
			var newCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);
			var cookies = CombineCookies(previousCookies, newCookies);

			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var sid = GetSid(clientSideResponse, jsonDoc.RootElement);

			var formattedPostcode = ProcessingUtilities.FormatPostcode(address.Postcode!);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"https://my.northdevon.gov.uk/apibroker/location?app_name=AF-Renderer::Self&sid={sid}",
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
						{ "postcode", formattedPostcode },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting address details
		else if (clientSideResponse.RequestId == 3)
		{
			var previousCookies = clientSideResponse.Options.Metadata.GetValueOrDefault("cookies", string.Empty);
			var newCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);
			var cookies = CombineCookies(previousCookies, newCookies);

			var sid = clientSideResponse.Options.Metadata.GetValueOrDefault("sid", string.Empty);
			var formattedPostcode = clientSideResponse.Options.Metadata.GetValueOrDefault("postcode", string.Empty);

			var requestBody = JsonSerializer.Serialize(new
			{
				stopOnFailure = true,
				usePHPIntegrations = true,
				stage_id = _stageId,
				stage_name = "Stage 1",
				formId = _formId,
				formValues = new Dictionary<string, object?>
				{
					["Your address"] = new
					{
						postcode_search = new { value = formattedPostcode },
						chooseAddress = new { value = address.Uid },
						uprnfromlookup = new { value = address.Uid },
						UPRNMF = new { value = address.Uid },
						FULLADDR2 = new { value = string.Empty },
					},
				},
				isPublished = true,
				formName = "WasteRecyclingCalendarForm",
				processId = _processId,
				tokens = new
				{
					site_url = _serviceUrl,
					site_path = "/service/WasteRecyclingCollectionCalendar",
					site_origin = "https://my.northdevon.gov.uk",
					product = "Self",
					formLanguage = "en",
					session_id = sid,
					formId = _formId,
					topFormId = _formId,
					parentFormId = _formId,
					formName = "WasteRecyclingCalendarForm",
					topFormName = "WasteRecyclingCalendarForm",
					parentFormName = "WasteRecyclingCalendarForm",
					processId = _processId,
					processName = "WasteRecyclingCollectionCalendar",
				},
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 4,
				Url = $"https://my.northdevon.gov.uk/apibroker/runLookup?id=65141c7c38bd0&repeat_against=&noRetry=true&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&sid={sid}",
				Method = "POST",
				Body = requestBody,
				Headers = new()
				{
					{ "content-type", "application/json" },
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
						{ "postcode", formattedPostcode },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting live token
		else if (clientSideResponse.RequestId == 4)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rows = jsonDoc.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("rows_data")
				.GetProperty("0");

			var fullAddress2 = rows.GetProperty("FULLADDR2").GetString()!.Trim();
			var usrn = rows.GetProperty("USRN").GetString()!.Trim();

			var cookies = clientSideResponse.Options.Metadata.GetValueOrDefault("cookies", string.Empty);
			var sid = clientSideResponse.Options.Metadata.GetValueOrDefault("sid", string.Empty);
			var formattedPostcode = clientSideResponse.Options.Metadata.GetValueOrDefault("postcode", string.Empty);

			var requestBody = JsonSerializer.Serialize(new
			{
				stopOnFailure = true,
				usePHPIntegrations = true,
				stage_id = _stageId,
				stage_name = "Stage 1",
				formId = _formId,
				formValues = new Dictionary<string, object?>
				{
					["Your address"] = new
					{
						postcode_search = new { value = formattedPostcode },
						chooseAddress = new { value = address.Uid },
						uprnfromlookup = new { value = address.Uid },
						UPRNMF = new { value = address.Uid },
						FULLADDR2 = new { value = fullAddress2 },
					},
				},
				isPublished = true,
				formName = "WasteRecyclingCalendarForm",
				processId = _processId,
				tokens = new
				{
					site_url = _serviceUrl,
					site_path = "/service/WasteRecyclingCollectionCalendar",
					site_origin = "https://my.northdevon.gov.uk",
					product = "Self",
					formLanguage = "en",
					session_id = sid,
					formId = _formId,
					topFormId = _formId,
					parentFormId = _formId,
					formName = "WasteRecyclingCalendarForm",
					topFormName = "WasteRecyclingCalendarForm",
					parentFormName = "WasteRecyclingCalendarForm",
					processId = _processId,
					processName = "WasteRecyclingCollectionCalendar",
				},
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 5,
				Url = $"https://my.northdevon.gov.uk/apibroker/runLookup?id=59e606ee95b7a&repeat_against=&noRetry=true&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&sid={sid}",
				Method = "POST",
				Body = requestBody,
				Headers = new()
				{
					{ "content-type", "application/json" },
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
						{ "postcode", formattedPostcode },
						{ "fullAddress2", fullAddress2 },
						{ "usrn", usrn },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting full address details
		else if (clientSideResponse.RequestId == 5)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var liveToken = jsonDoc.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("rows_data")
				.GetProperty("0")
				.GetProperty("liveToken")
				.GetString()!
				.Trim();

			var metadata = new Dictionary<string, string>(clientSideResponse.Options.Metadata)
			{
				{ "liveToken", liveToken },
			};

			var cookies = metadata.GetValueOrDefault("cookies", string.Empty);
			var sid = metadata.GetValueOrDefault("sid", string.Empty);
			var formattedPostcode = metadata.GetValueOrDefault("postcode", string.Empty);
			var fullAddress2 = metadata.GetValueOrDefault("fullAddress2", string.Empty);

			var requestBody = JsonSerializer.Serialize(new
			{
				stopOnFailure = true,
				usePHPIntegrations = true,
				stage_id = _stageId,
				stage_name = "Stage 1",
				formId = _formId,
				formValues = new Dictionary<string, object?>
				{
					["Your address"] = new
					{
						postcode_search = new { value = formattedPostcode },
						chooseAddress = new { value = address.Uid },
						uprnfromlookup = new { value = address.Uid },
						UPRNMF = new { value = address.Uid },
						FULLADDR2 = new { value = fullAddress2 },
					},
				},
				isPublished = true,
				formName = "WasteRecyclingCalendarForm",
				processId = _processId,
				tokens = new
				{
					site_url = _serviceUrl,
					site_path = "/service/WasteRecyclingCollectionCalendar",
					site_origin = "https://my.northdevon.gov.uk",
					product = "Self",
					formLanguage = "en",
					session_id = sid,
					formId = _formId,
					topFormId = _formId,
					parentFormId = _formId,
					formName = "WasteRecyclingCalendarForm",
					topFormName = "WasteRecyclingCalendarForm",
					parentFormName = "WasteRecyclingCalendarForm",
					processId = _processId,
					processName = "WasteRecyclingCollectionCalendar",
				},
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 6,
				Url = $"https://my.northdevon.gov.uk/apibroker/runLookup?id=625587f465a91&repeat_against=&noRetry=true&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&sid={sid}",
				Method = "POST",
				Body = requestBody,
				Headers = new()
				{
					{ "content-type", "application/json" },
					{ "cookie", cookies },
					{ "x-requested-with", "XMLHttpRequest" },
					{ "User-Agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions
				{
					Metadata = metadata,
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting calendar window
		else if (clientSideResponse.RequestId == 6)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rows = jsonDoc.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("rows_data")
				.GetProperty("0");

			var fullAddress = rows.GetProperty("FULLADDR").GetString()!.Trim();
			var usrn = rows.GetProperty("USRN").GetString()!.Trim();

			var metadata = new Dictionary<string, string>(clientSideResponse.Options.Metadata)
			{
				["fullAddress"] = fullAddress,
				["usrn"] = usrn,
			};

			var cookies = metadata.GetValueOrDefault("cookies", string.Empty);
			var sid = metadata.GetValueOrDefault("sid", string.Empty);
			var formattedPostcode = metadata.GetValueOrDefault("postcode", string.Empty);
			var fullAddress2 = metadata.GetValueOrDefault("fullAddress2", string.Empty);

			var requestBody = JsonSerializer.Serialize(new
			{
				stopOnFailure = true,
				usePHPIntegrations = true,
				stage_id = _stageId,
				stage_name = "Stage 1",
				formId = _formId,
				formValues = new Dictionary<string, object?>
				{
					["Your address"] = new
					{
						postcode_search = new { value = formattedPostcode },
						chooseAddress = new { value = address.Uid },
						uprnfromlookup = new { value = address.Uid },
						UPRNMF = new { value = address.Uid },
						FULLADDR2 = new { value = fullAddress2 },
					},
				},
				isPublished = true,
				formName = "WasteRecyclingCalendarForm",
				processId = _processId,
				tokens = new
				{
					site_url = _serviceUrl,
					site_path = "/service/WasteRecyclingCollectionCalendar",
					site_origin = "https://my.northdevon.gov.uk",
					product = "Self",
					formLanguage = "en",
					session_id = sid,
					formId = _formId,
					topFormId = _formId,
					parentFormId = _formId,
					formName = "WasteRecyclingCalendarForm",
					topFormName = "WasteRecyclingCalendarForm",
					parentFormName = "WasteRecyclingCalendarForm",
					processId = _processId,
					processName = "WasteRecyclingCollectionCalendar",
				},
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 7,
				Url = $"https://my.northdevon.gov.uk/apibroker/runLookup?id=6255925ca44cb&repeat_against=&noRetry=true&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&sid={sid}",
				Method = "POST",
				Body = requestBody,
				Headers = new()
				{
					{ "content-type", "application/json" },
					{ "cookie", cookies },
					{ "x-requested-with", "XMLHttpRequest" },
					{ "User-Agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions
				{
					Metadata = metadata,
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting collection results
		else if (clientSideResponse.RequestId == 7)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rows = jsonDoc.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("rows_data")
				.GetProperty("0");

			var calendarStartDate = rows.GetProperty("calstartDate").GetString()!.Trim();
			var calendarEndDate = rows.GetProperty("calendDate").GetString()!.Trim();

			var metadata = new Dictionary<string, string>(clientSideResponse.Options.Metadata)
			{
				{ "calendarStartDate", calendarStartDate },
				{ "calendarEndDate", calendarEndDate },
			};

			var liveToken = metadata.GetValueOrDefault("liveToken", string.Empty);
			var clientSideRequest = CreateCalendarLookupRequest(8, "61091d927cd81", liveToken, metadata, address, noRetry: false);

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 8)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rowsData = jsonDoc.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("rows_data");

			var binDaysByDate = ParseBinDaysFromRowsData(rowsData);

			if (binDaysByDate.Count == 0)
			{
				var metadata = clientSideResponse.Options.Metadata;
				var liveToken = metadata.GetValueOrDefault("liveToken", string.Empty);
				var clientSideRequest = CreateCalendarLookupRequest(9, "61091d927cd81", liveToken, metadata, address, noRetry: false);

				var getBinDaysResponseEmpty = new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest,
				};

				return getBinDaysResponseEmpty;
			}

			var binDays = binDaysByDate
				.Select(kvp => new BinDay
				{
					Date = kvp.Key,
					Address = address,
					Bins = [.. kvp.Value],
				})
				.ToList();

			var getBinDaysResponse = new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};

			return getBinDaysResponse;
		}
		// Process bin days from retry response (used when initial request returns no data)
		else if (clientSideResponse.RequestId == 9)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rowsData = jsonDoc.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("rows_data");

			var binDaysByDate = ParseBinDaysFromRowsData(rowsData);

			var binDays = binDaysByDate
				.Select(kvp => new BinDay
				{
					Date = kvp.Key,
					Address = address,
					Bins = [.. kvp.Value],
				})
				.ToList();

			var getBinDaysResponse = new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};

			return getBinDaysResponse;
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <summary>
	/// Builds the request body for calendar lookup requests.
	/// Centralizes the complex request body structure used for fetching bin collection data.
	/// </summary>
	private static string BuildCalendarRequestBody(
		Address address,
		string formattedPostcode,
		string fullAddress2,
		string fullAddress,
		string usrn,
		string tokenValue,
		string liveToken,
		string calendarStartDate,
		string calendarEndDate,
		string sid,
		IEnumerable<object>? details = null)
	{
		return JsonSerializer.Serialize(new
		{
			stopOnFailure = true,
			usePHPIntegrations = true,
			stage_id = _stageId,
			stage_name = "Stage 1",
			formId = _formId,
			formValues = new Dictionary<string, object?>
			{
				["Your address"] = new
				{
					postcode_search = new { value = formattedPostcode },
					chooseAddress = new { value = address.Uid },
					uprnfromlookup = new { value = address.Uid },
					UPRNMF = new { value = address.Uid },
					FULLADDR2 = new { value = fullAddress2 },
				},
				["Calendar"] = new Dictionary<string, object?>
				{
					{ "FULLADDR", new { value = fullAddress } },
					{ "token", new { value = tokenValue } },
					{ "uPRN", new { value = address.Uid } },
					{ "calstartDate", new { value = calendarStartDate } },
					{ "calendDate", new { value = calendarEndDate } },
					{ "details", details ?? [] },
					{ "text1", new { value = string.Empty } },
					{ "Results", new { value = string.Empty } },
					{ "UPRN", new { value = address.Uid } },
					{ "Alerts", new { value = string.Empty } },
					{ "liveToken", new { value = liveToken } },
					{ "Results2", new { value = string.Empty } },
					{ "USRN", new { value = usrn } },
					{ "streetEvents", Array.Empty<object>() },
					{ "EventDescription", new { value = string.Empty } },
					{ "EventDate", new { value = string.Empty } },
					{ "EventsDisplay", new { value = string.Empty } },
					{ "Comments", new { value = string.Empty } },
					{ "OutText", new { value = string.Empty } },
					{ "StartDate", new { value = calendarStartDate } },
					{ "EndDate", new { value = calendarEndDate } },
				},
				["Print version"] = new
				{
					OutText2 = new { value = string.Empty },
				},
			},
			isPublished = true,
			formName = "WasteRecyclingCalendarForm",
			processId = _processId,
			tokens = new
			{
				site_url = _serviceUrl,
				site_path = "/service/WasteRecyclingCollectionCalendar",
				site_origin = "https://my.northdevon.gov.uk",
				product = "Self",
				formLanguage = "en",
				session_id = sid,
				formId = _formId,
				topFormId = _formId,
				parentFormId = _formId,
				formName = "WasteRecyclingCalendarForm",
				topFormName = "WasteRecyclingCalendarForm",
				parentFormName = "WasteRecyclingCalendarForm",
				processId = _processId,
				processName = "WasteRecyclingCollectionCalendar",
			},
		});
	}

	/// <summary>
	/// Creates a calendar lookup request with the specified parameters.
	/// Used for requesting bin collection schedules from the API.
	/// </summary>
	private static ClientSideRequest CreateCalendarLookupRequest(
		int requestId,
		string lookupId,
		string tokenValue,
		IReadOnlyDictionary<string, string> metadata,
		Address address,
		bool noRetry = true,
		IEnumerable<object>? detailsOverride = null)
	{
		var cookies = metadata.GetValueOrDefault("cookies", string.Empty);
		var sid = metadata.GetValueOrDefault("sid", string.Empty);
		var formattedPostcode = metadata.GetValueOrDefault("postcode", string.Empty);
		var fullAddress2 = metadata.GetValueOrDefault("fullAddress2", string.Empty);
		var fullAddress = metadata.GetValueOrDefault("fullAddress", fullAddress2);
		var usrn = metadata.GetValueOrDefault("usrn", string.Empty);
		var liveToken = metadata.GetValueOrDefault("liveToken", string.Empty);
		var calendarStartDate = metadata.GetValueOrDefault("calendarStartDate", string.Empty);
		var calendarEndDate = metadata.GetValueOrDefault("calendarEndDate", string.Empty);
		var metadataDictionary = new Dictionary<string, string>(metadata);
		var details = detailsOverride;

		if (details == null && metadataDictionary.TryGetValue("details", out var detailsJson) && !string.IsNullOrWhiteSpace(detailsJson))
		{
			details = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(detailsJson);
		}

		var requestBody = BuildCalendarRequestBody(
			address,
			formattedPostcode,
			fullAddress2,
			fullAddress,
			usrn,
			tokenValue,
			liveToken,
			calendarStartDate,
			calendarEndDate,
			sid,
			details);

		return CreateRunLookupRequest(
			requestId,
			lookupId,
			sid,
			cookies,
			requestBody,
			metadataDictionary,
			noRetry);
	}

	/// <summary>
	/// Creates a run lookup request for the API broker.
	/// Encapsulates the common pattern for API broker requests with timestamp and retry parameters.
	/// </summary>
	private static ClientSideRequest CreateRunLookupRequest(
		int requestId,
		string lookupId,
		string sid,
		string cookies,
		string requestBody,
		Dictionary<string, string> metadata,
		bool noRetry = true)
	{
		var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		var noRetryValue = noRetry ? "true" : "false";

		return new ClientSideRequest
		{
			RequestId = requestId,
			Url = $"https://my.northdevon.gov.uk/apibroker/runLookup?id={lookupId}&repeat_against=&noRetry={noRetryValue}&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&_={timestamp}&sid={sid}",
			Method = "POST",
			Body = requestBody,
			Headers = new()
			{
				{ "content-type", "application/json" },
				{ "cookie", cookies },
				{ "x-requested-with", "XMLHttpRequest" },
				{ "User-Agent", Constants.UserAgent },
			},
			Options = new ClientSideOptions
			{
				Metadata = metadata,
			},
		};
	}

	private static string CombineCookies(params string?[] cookieStrings)
	{
		return string.Join("; ", cookieStrings.Where(c => !string.IsNullOrWhiteSpace(c)));
	}

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
	/// <param name="clientSideResponse">The client-side response, or null to start the flow.</param>
	/// <param name="nextRequestId">The request ID to assign to the next request (3 for GetAddresses, 3 for GetBinDays).</param>
	/// <returns>A tuple containing the next client-side request and a flag indicating if the flow should continue.</returns>
	private static (ClientSideRequest? clientSideRequest, bool shouldContinue) HandleSessionInitialization(
		ClientSideResponse? clientSideResponse,
		int nextRequestId)
	{
		// Step 1: Get initial session cookies
		if (clientSideResponse == null)
		{
			return (new ClientSideRequest
			{
				RequestId = 1,
				Url = _serviceUrl,
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
			if (!clientSideResponse.Headers.TryGetValue("set-cookie", out var value))
			{
				throw new InvalidOperationException("Expected set-cookie header not found in response.");
			}

			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(value);

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
