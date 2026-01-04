namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors.Vendors;
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Text.Json;
	using System.Text.RegularExpressions;
	using System.Web;

	/// <summary>
	/// Collector implementation for North Devon Council.
	/// </summary>
	internal sealed partial class NorthDevonCouncil : GovUkCollectorBase, ICollector
	{
		private const string ServiceUrl = "https://my.northdevon.gov.uk/service/WasteRecyclingCollectionCalendar";
		private const string StageId = "AF-Stage-0e576350-a6e1-444e-a105-cb020f910845";
		private const string FormId = "AF-Form-a9a357e7-8b6d-416e-b974-04a2aa857e87";
		private const string ProcessId = "AF-Process-d615d6eb-6718-4e33-a2ff-18f1e5e58f8b";

		/// <inheritdoc/>
		public string Name => "North Devon Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.northdevon.gov.uk/");

		/// <inheritdoc/>
		public override string GovUkId => "north-devon";

		/// <summary>
		/// Regex to match month sections and their collection items.
		/// </summary>
		[GeneratedRegex("<h4>(?<monthYear>[A-Za-z]+\\s+\\d{4})</h4>(?<items>.*?)(?=<h4>[A-Za-z]+\\s+\\d{4}</h4>|<h2>|$)", RegexOptions.Singleline)]
		private static partial Regex MonthSectionRegex();

		/// <summary>
		/// Regex to match individual collection entries.
		/// </summary>
		[GeneratedRegex("<li class=\\\"(?<binKey>[^\\\"]+)\\\"[^>]*>\\s*<span class=\\\"wasteName\\\">(?<dayName>[^<]+)</span>\\s*<span class=\\\"wasteDay\\\">(?<day>\\d{2})</span>\\s*<span class=\\\"wasteType\\\">(?<binLabel>[^<]+)</span>", RegexOptions.Singleline)]
		private static partial Regex CollectionItemRegex();

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly IReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "General Waste",
				Colour = BinColour.Black,
				Type = BinType.Bin,
				Keys = new List<string>() { "Black Bin", "BlackBin" }.AsReadOnly(),
			},
			new()
			{
				Name = "Recycling",
				Colour = BinColour.Green,
				Type = BinType.Container,
				Keys = new List<string>() { "Recycling" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Step 1: Load the service to establish session cookies.
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = ServiceUrl,
					Method = "GET",
					Headers = new Dictionary<string, string>
					{
						{ "User-Agent", Constants.UserAgent },
					},
					Options = new ClientSideOptions
					{
						Metadata = new Dictionary<string, string>(),
					},
				};

				return new GetAddressesResponse
				{
					NextClientSideRequest = clientSideRequest,
				};
			}

			// Step 2: Fetch auth session (sid) using existing cookies.
			if (clientSideResponse.RequestId == 1)
			{
				var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 2,
					Url = "https://my.northdevon.gov.uk/authapi/isauthenticated?uri=https%3A%2F%2Fmy.northdevon.gov.uk%2Fservice%2FWasteRecyclingCollectionCalendar&hostname=my.northdevon.gov.uk&withCredentials=true",
					Method = "GET",
					Headers = new Dictionary<string, string>
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
				};

				return new GetAddressesResponse
				{
					NextClientSideRequest = clientSideRequest,
				};
			}

			// Step 3: Get location data with the established sid.
			if (clientSideResponse.RequestId == 2)
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
					Headers = new Dictionary<string, string>
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

				return new GetAddressesResponse
				{
					NextClientSideRequest = clientSideRequest,
				};
			}

			// Step 4: Look up addresses for the given postcode.
			if (clientSideResponse.RequestId == 3)
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
					stage_id = StageId,
					stage_name = "Stage 1",
					formId = FormId,
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
					processId = ProcessId,
					tokens = new
					{
						site_url = ServiceUrl,
						site_path = "/service/WasteRecyclingCollectionCalendar",
						site_origin = "https://my.northdevon.gov.uk",
						product = "Self",
						formLanguage = "en",
						session_id = sid,
						formId = FormId,
						topFormId = FormId,
						parentFormId = FormId,
						formName = "WasteRecyclingCalendarForm",
						topFormName = "WasteRecyclingCalendarForm",
						parentFormName = "WasteRecyclingCalendarForm",
						processId = ProcessId,
						processName = "WasteRecyclingCollectionCalendar",
					},
				});

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 4,
					Url = $"https://my.northdevon.gov.uk/apibroker/runLookup?id=5849617f4ce25&repeat_against=&noRetry=false&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&sid={sid}",
					Method = "POST",
					Body = requestBody,
					Headers = new Dictionary<string, string>
					{
						{ "content-type", "application/json" },
						{ "cookie", cookies },
						{ "x-requested-with", "XMLHttpRequest" },
						{ "User-Agent", Constants.UserAgent },
					},
				};

				return new GetAddressesResponse
				{
					NextClientSideRequest = clientSideRequest,
				};
			}

			// Step 5: Parse address list.
			if (clientSideResponse.RequestId == 4)
			{
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
				var rowsData = jsonDoc.RootElement
					.GetProperty("integration")
					.GetProperty("transformed")
					.GetProperty("rows_data");

				var addresses = new List<Address>();

				if (rowsData.ValueKind == JsonValueKind.Array)
				{
					return new GetAddressesResponse
					{
						Addresses = addresses.AsReadOnly(),
					};
				}

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

				return new GetAddressesResponse
				{
					Addresses = addresses.AsReadOnly(),
				};
			}

			throw new InvalidOperationException("Invalid client-side request.");
		}

		/// <inheritdoc/>
		public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
		{
			// Step 1: Load the service page to set cookies.
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = ServiceUrl,
					Method = "GET",
					Headers = new Dictionary<string, string>
					{
						{ "User-Agent", Constants.UserAgent },
					},
					Options = new ClientSideOptions
					{
						Metadata = new Dictionary<string, string>(),
					},
				};

				return new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest,
				};
			}

			// Step 2: Get auth session id (sid) using current cookies.
			if (clientSideResponse.RequestId == 1)
			{
				var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 2,
					Url = "https://my.northdevon.gov.uk/authapi/isauthenticated?uri=https%3A%2F%2Fmy.northdevon.gov.uk%2Fservice%2FWasteRecyclingCollectionCalendar&hostname=my.northdevon.gov.uk&withCredentials=true",
					Method = "GET",
					Headers = new Dictionary<string, string>
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
				};

				return new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest,
				};
			}

			// Step 3: Retrieve location data with the sid.
			if (clientSideResponse.RequestId == 2)
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
					Headers = new Dictionary<string, string>
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

				return new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest,
				};
			}

			// Step 4: Retrieve FULLADDR2 and USRN for the selected address.
			if (clientSideResponse.RequestId == 3)
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
					stage_id = StageId,
					stage_name = "Stage 1",
					formId = FormId,
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
					processId = ProcessId,
					tokens = new
					{
						site_url = ServiceUrl,
						site_path = "/service/WasteRecyclingCollectionCalendar",
						site_origin = "https://my.northdevon.gov.uk",
						product = "Self",
						formLanguage = "en",
						session_id = sid,
						formId = FormId,
						topFormId = FormId,
						parentFormId = FormId,
						formName = "WasteRecyclingCalendarForm",
						topFormName = "WasteRecyclingCalendarForm",
						parentFormName = "WasteRecyclingCalendarForm",
						processId = ProcessId,
						processName = "WasteRecyclingCollectionCalendar",
					},
				});

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 4,
					Url = $"https://my.northdevon.gov.uk/apibroker/runLookup?id=65141c7c38bd0&repeat_against=&noRetry=true&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&sid={sid}",
					Method = "POST",
					Body = requestBody,
					Headers = new Dictionary<string, string>
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

				return new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest,
				};
			}

			// Step 4: Fetch live token using address details.
			if (clientSideResponse.RequestId == 4)
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
					stage_id = StageId,
					stage_name = "Stage 1",
					formId = FormId,
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
					processId = ProcessId,
					tokens = new
					{
						site_url = ServiceUrl,
						site_path = "/service/WasteRecyclingCollectionCalendar",
						site_origin = "https://my.northdevon.gov.uk",
						product = "Self",
						formLanguage = "en",
						session_id = sid,
						formId = FormId,
						topFormId = FormId,
						parentFormId = FormId,
						formName = "WasteRecyclingCalendarForm",
						topFormName = "WasteRecyclingCalendarForm",
						parentFormName = "WasteRecyclingCalendarForm",
						processId = ProcessId,
						processName = "WasteRecyclingCollectionCalendar",
					},
				});

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 5,
					Url = $"https://my.northdevon.gov.uk/apibroker/runLookup?id=59e606ee95b7a&repeat_against=&noRetry=true&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&sid={sid}",
					Method = "POST",
					Body = requestBody,
					Headers = new Dictionary<string, string>
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

				return new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest,
				};
			}

			// Step 5: Retrieve FULLADDR and USRN.
			if (clientSideResponse.RequestId == 5)
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
					stage_id = StageId,
					stage_name = "Stage 1",
					formId = FormId,
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
					processId = ProcessId,
					tokens = new
					{
						site_url = ServiceUrl,
						site_path = "/service/WasteRecyclingCollectionCalendar",
						site_origin = "https://my.northdevon.gov.uk",
						product = "Self",
						formLanguage = "en",
						session_id = sid,
						formId = FormId,
						topFormId = FormId,
						parentFormId = FormId,
						formName = "WasteRecyclingCalendarForm",
						topFormName = "WasteRecyclingCalendarForm",
						parentFormName = "WasteRecyclingCalendarForm",
						processId = ProcessId,
						processName = "WasteRecyclingCollectionCalendar",
					},
				});

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 6,
					Url = $"https://my.northdevon.gov.uk/apibroker/runLookup?id=625587f465a91&repeat_against=&noRetry=true&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&sid={sid}",
					Method = "POST",
					Body = requestBody,
					Headers = new Dictionary<string, string>
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

				return new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest,
				};
			}

			// Step 6: Fetch calendar start and end dates.
			if (clientSideResponse.RequestId == 6)
			{
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
				var rows = jsonDoc.RootElement
					.GetProperty("integration")
					.GetProperty("transformed")
					.GetProperty("rows_data")
					.GetProperty("0");

				var fullAddress = rows.GetProperty("FULLADDR").GetString()!.Trim();
				var usrn = rows.GetProperty("USRN").GetString()!.Trim();

				var metadata = new Dictionary<string, string>(clientSideResponse.Options.Metadata);
				metadata["fullAddress"] = fullAddress;
				metadata["usrn"] = usrn;

				var cookies = metadata.GetValueOrDefault("cookies", string.Empty);
				var sid = metadata.GetValueOrDefault("sid", string.Empty);
				var formattedPostcode = metadata.GetValueOrDefault("postcode", string.Empty);
				var fullAddress2 = metadata.GetValueOrDefault("fullAddress2", string.Empty);

				var requestBody = JsonSerializer.Serialize(new
				{
					stopOnFailure = true,
					usePHPIntegrations = true,
					stage_id = StageId,
					stage_name = "Stage 1",
					formId = FormId,
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
					processId = ProcessId,
					tokens = new
					{
						site_url = ServiceUrl,
						site_path = "/service/WasteRecyclingCollectionCalendar",
						site_origin = "https://my.northdevon.gov.uk",
						product = "Self",
						formLanguage = "en",
						session_id = sid,
						formId = FormId,
						topFormId = FormId,
						parentFormId = FormId,
						formName = "WasteRecyclingCalendarForm",
						topFormName = "WasteRecyclingCalendarForm",
						parentFormName = "WasteRecyclingCalendarForm",
						processId = ProcessId,
						processName = "WasteRecyclingCollectionCalendar",
					},
				});

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 7,
					Url = $"https://my.northdevon.gov.uk/apibroker/runLookup?id=6255925ca44cb&repeat_against=&noRetry=true&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&sid={sid}",
					Method = "POST",
					Body = requestBody,
					Headers = new Dictionary<string, string>
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

				return new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest,
				};
			}

			// Step 7: Retrieve calendar window and kick off result lookups.
			if (clientSideResponse.RequestId == 7)
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

				return new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest,
				};
			}

			// Step 8: Parse work pack data into bin days.
			if (clientSideResponse.RequestId == 8)
			{
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
				var rowsData = jsonDoc.RootElement
					.GetProperty("integration")
					.GetProperty("transformed")
					.GetProperty("rows_data");

				var binDaysByDate = new Dictionary<DateOnly, HashSet<Bin>>();
				var generalWasteBin = _binTypes.First(b => b.Name == "General Waste");
				var recyclingBin = _binTypes.First(b => b.Name == "Recycling");

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

						var match = Regex.Match(serviceDetail, @"(?<label>.+)/(?<date>\d{2}/\d{2}/\d{4})");

						if (!match.Success)
						{
							continue;
						}

						var collectionDate = DateOnly.ParseExact(match.Groups["date"].Value, "dd/MM/yyyy", CultureInfo.InvariantCulture);
						var binsForDate = binDaysByDate.GetValueOrDefault(collectionDate, new HashSet<Bin>());

						if (workPack.StartsWith("Waste-Black", StringComparison.OrdinalIgnoreCase))
						{
							binsForDate.Add(generalWasteBin);
						}
						else if (workPack.StartsWith("Waste-Recycling", StringComparison.OrdinalIgnoreCase))
						{
							binsForDate.Add(recyclingBin);
						}
						else
						{
							continue;
						}

						binDaysByDate[collectionDate] = binsForDate;
					}
				}

				if (binDaysByDate.Count == 0)
				{
					var metadata = clientSideResponse.Options.Metadata;
					var liveToken = metadata.GetValueOrDefault("liveToken", string.Empty);
					var clientSideRequest = CreateCalendarLookupRequest(9, "61091d927cd81", liveToken, metadata, address, noRetry: false);

					return new GetBinDaysResponse
					{
						NextClientSideRequest = clientSideRequest,
					};
				}

				var binDays = binDaysByDate
					.Select(kvp => new BinDay
					{
						Date = kvp.Key,
						Address = address,
						Bins = kvp.Value.ToList(),
					})
					.ToList();

				return new GetBinDaysResponse
				{
					BinDays = ProcessingUtilities.ProcessBinDays(binDays),
				};
			}

			if (clientSideResponse.RequestId == 9)
			{
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
				var rowsData = jsonDoc.RootElement
					.GetProperty("integration")
					.GetProperty("transformed")
					.GetProperty("rows_data");

				var binDaysByDate = new Dictionary<DateOnly, HashSet<Bin>>();
				var generalWasteBin = _binTypes.First(b => b.Name == "General Waste");
				var recyclingBin = _binTypes.First(b => b.Name == "Recycling");

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

						var match = Regex.Match(serviceDetail, @"(?<label>.+)/(?<date>\d{2}/\d{2}/\d{4})");

						if (!match.Success)
						{
							continue;
						}

						var collectionDate = DateOnly.ParseExact(match.Groups["date"].Value, "dd/MM/yyyy", CultureInfo.InvariantCulture);
						var binsForDate = binDaysByDate.GetValueOrDefault(collectionDate, new HashSet<Bin>());

						if (workPack.StartsWith("Waste-Black", StringComparison.OrdinalIgnoreCase))
						{
							binsForDate.Add(generalWasteBin);
						}
						else if (workPack.StartsWith("Waste-Recycling", StringComparison.OrdinalIgnoreCase))
						{
							binsForDate.Add(recyclingBin);
						}
						else
						{
							continue;
						}

						binDaysByDate[collectionDate] = binsForDate;
					}
				}

				var binDays = binDaysByDate
					.Select(kvp => new BinDay
					{
						Date = kvp.Key,
						Address = address,
						Bins = kvp.Value.ToList(),
					})
					.ToList();

				return new GetBinDaysResponse
				{
					BinDays = ProcessingUtilities.ProcessBinDays(binDays),
				};
			}

			throw new InvalidOperationException("Invalid client-side request.");
		}

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
				stage_id = StageId,
				stage_name = "Stage 1",
				formId = FormId,
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
							{ "details", details ?? Array.Empty<object>() },
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
				processId = ProcessId,
				tokens = new
				{
					site_url = ServiceUrl,
					site_path = "/service/WasteRecyclingCollectionCalendar",
					site_origin = "https://my.northdevon.gov.uk",
					product = "Self",
					formLanguage = "en",
					session_id = sid,
					formId = FormId,
					topFormId = FormId,
					parentFormId = FormId,
					formName = "WasteRecyclingCalendarForm",
					topFormName = "WasteRecyclingCalendarForm",
					parentFormName = "WasteRecyclingCalendarForm",
					processId = ProcessId,
					processName = "WasteRecyclingCollectionCalendar",
				},
			});
		}

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
			IEnumerable<object>? details = detailsOverride;

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
				Headers = new Dictionary<string, string>
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
	}
}
