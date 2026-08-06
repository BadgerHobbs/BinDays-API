namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Cheltenham Borough Council.
/// </summary>
internal sealed partial class CheltenhamBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Cheltenham Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.cheltenham.gov.uk/bin-collection-days");

	/// <inheritdoc/>
	public override string GovUkId => "cheltenham";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "Refuse bin" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Green,
			Keys = [ "Green recycling box" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Cardboard & Paper Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Blue cardboard bag" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden waste bin" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Food caddy" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// The URL for the bin lookup page, and its underlying form submission endpoint.
	/// </summary>
	private const string _lookupPageUrl = "https://cheltenham-host01.oncreate.app/w/webpage/collection-lookup";

	/// <summary>
	/// The page id of the lookup page.
	/// </summary>
	private const string _pageId = "PAG0000686GBCNH1";

	/// <summary>
	/// The widget group id of the address search form.
	/// </summary>
	private const string _widgetGroupId = "PWG0002596GBCNH1";

	/// <summary>
	/// The cell id containing the address search form.
	/// </summary>
	private const string _cellId = "PCL0005127GBCNH1";

	/// <summary>
	/// The fragment id of the address selector field.
	/// </summary>
	private const string _addressFieldId = "PCF0019732GBCNH1";

	/// <summary>
	/// The fragment id of the "Next" submit button.
	/// </summary>
	private const string _nextFieldId = "PCF0016614GBCNH1";

	/// <summary>
	/// Regex for the CSRF token.
	/// </summary>
	[GeneratedRegex(@"var CSRF = '(?<token>[^']+)'")]
	private static partial Regex CsrfTokenRegex();

	/// <summary>
	/// Regex for the webpage token.
	/// </summary>
	[GeneratedRegex(@"webpage_token=(?<token>[a-f0-9]+)")]
	private static partial Regex WebpageTokenRegex();

	/// <summary>
	/// Regex for the dynamically generated fragment collection key that wraps the address
	/// search field. This value changes on every page load, so it must be extracted rather
	/// than hardcoded.
	/// </summary>
	[GeneratedRegex(@"data-class_name=""search"" data-unique_key=""(?<key>C_[a-f0-9]+)""")]
	private static partial Regex UniqueKeyRegex();

	/// <summary>
	/// Regex for the submission token.
	/// </summary>
	[GeneratedRegex(@"name=""submission_token"" value=""(?<token>[^""]+)""")]
	private static partial Regex SubmissionTokenRegex();

	/// <summary>
	/// Regex for the "Search for bin collection dates" trigger-event link.
	/// </summary>
	[GeneratedRegex(@"href=""(?<url>/w/webpage/collection-lookup\?do_action=trigger_event[^""]+)""")]
	private static partial Regex TriggerEventUrlRegex();

	/// <summary>
	/// Regex for the bin days table rows, matching the container type and next collection date.
	/// </summary>
	[GeneratedRegex(@"data-fragment_id=""PCF0019703GBCNH1""[^>]*data-current_value=""(?<service>[^""]+)""[\s\S]*?data-fragment_id=""PCF0019810GBCNH1""[^>]*data-current_value=""(?<date>\d{2}/\d{2}/\d{4})""")]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _lookupPageUrl,
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for the address search
		else if (clientSideResponse.RequestId == 1)
		{
			var csrfToken = CsrfTokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;
			var pageToken = WebpageTokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "code_action", "search" },
				{ "code_params", $$"""{"search_item":"{{postcode}}"}""" },
				{ "fragment_action", "handle_event" },
				{ "fragment_id", _addressFieldId },
				{ "fragment_collection_class", "search" },
				{ "action_cell_id", _cellId },
				{ "action_page_id", _pageId },
				{ "form_check_ajax", csrfToken },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_lookupPageUrl}?webpage_subpage_id={_pageId}&webpage_token={pageToken}&widget_action=fragment_action",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
					{ "x-requested-with", Constants.XmlHttpRequest },
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
			var options = jsonDoc.RootElement.GetProperty("response").GetProperty("options");

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var option in options.EnumerateArray())
			{
				var address = new Address
				{
					Property = option.GetProperty("summary_address").GetString()!,
					Postcode = postcode,
					Uid = option.GetProperty("id").GetString()!,
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
				Url = _lookupPageUrl,
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request to render the address search form
		else if (clientSideResponse.RequestId == 1)
		{
			var csrfToken = CsrfTokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;
			var pageToken = WebpageTokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;
			var cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
				clientSideResponse.Headers["set-cookie"]);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = _lookupPageUrl,
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
					{ "x-requested-with", Constants.XmlHttpRequest },
					{ "cookie", cookie },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{ "_dummy", "1" },
				}),
				Options = new ClientSideOptions
				{
					Metadata = { { "csrfToken", csrfToken }, { "pageToken", pageToken }, { "cookie", cookie } },
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request to select the address and submit
		else if (clientSideResponse.RequestId == 2)
		{
			var csrfToken = clientSideResponse.Options.Metadata["csrfToken"];
			var pageToken = clientSideResponse.Options.Metadata["pageToken"];
			var setCookieHeader = clientSideResponse.Headers.GetValueOrDefault("set-cookie");
			var cookie = setCookieHeader == null
				? clientSideResponse.Options.Metadata["cookie"]
				: ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var formHtml = jsonDoc.RootElement.GetProperty("data").GetString()!;
			var uniqueKey = UniqueKeyRegex().Match(formHtml).Groups["key"].Value;
			var submissionToken = SubmissionTokenRegex().Match(formHtml).Groups["token"].Value;

			var payloadPrefix = $"payload[{_pageId}][{_widgetGroupId}][{_cellId}][search][{uniqueKey}]";
			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "form_check", csrfToken },
				{ "submitted_page_id", _pageId },
				{ "submitted_widget_group_id", _widgetGroupId },
				{ "submission_token", submissionToken },
				{ $"{payloadPrefix}[{_addressFieldId}]", address.Uid! },
				{ $"{payloadPrefix}[{_nextFieldId}]", "Next" },
				{ "submit_fragment_id", _nextFieldId },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"{_lookupPageUrl}?webpage_subpage_id={_pageId}&webpage_token={pageToken}",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
					{ "x-requested-with", Constants.XmlHttpRequest },
					{ "cookie", cookie },
				},
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata = { { "cookie", cookie }, { "csrfToken", csrfToken } },
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request to trigger the bin collection dates lookup
		else if (clientSideResponse.RequestId == 3)
		{
			var csrfToken = clientSideResponse.Options.Metadata["csrfToken"];
			var setCookieHeader = clientSideResponse.Headers.GetValueOrDefault("set-cookie");
			var cookie = setCookieHeader == null
				? clientSideResponse.Options.Metadata["cookie"]
				: ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var listingHtml = jsonDoc.RootElement.GetProperty("data").GetString()!;
			var triggerUrl = TriggerEventUrlRegex().Match(listingHtml).Groups["url"].Value.Replace("&amp;", "&");

			// The trigger-event action only flash-stores populated bin day data when the
			// selected address is echoed back as an active filter on the search field.
			var sessionStorage = $$"""
			{
				"/w/webpage/collection-lookup": {
					"filters": {
						"defined": {
							"OBJ0000008GBCNF1": [
								{
									"{{_pageId}}": {
										"{{_cellId}}": {
											"search": {
												"{{_addressFieldId}}": {
													"comparator_id": "4",
													"value": "{{address.Uid}}"
												}
											}
										}
									}
								}
							]
						}
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 4,
				Url = $"https://cheltenham-host01.oncreate.app{triggerUrl}",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
					{ "x-requested-with", Constants.XmlHttpRequest },
					{ "cookie", cookie },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{ "_session_storage", sessionStorage },
					{ "form_check_ajax", csrfToken },
				}),
				Options = new ClientSideOptions
				{
					FollowRedirects = false,
					Metadata = { { "cookie", cookie } },
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request to follow the redirect to the populated bin days page
		else if (clientSideResponse.RequestId == 4)
		{
			var cookie = clientSideResponse.Options.Metadata["cookie"];
			var location = clientSideResponse.Headers["location"];

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 5,
				Url = $"https://cheltenham-host01.oncreate.app{location}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "x-requested-with", Constants.XmlHttpRequest },
					{ "cookie", cookie },
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 5)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var resultsHtml = jsonDoc.RootElement.GetProperty("data").GetString()!;
			var rawBinDays = BinDaysRegex().Matches(resultsHtml)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["service"].Value.Trim();
				var date = DateUtilities.ParseDateExact(rawBinDay.Groups["date"].Value, "dd/MM/yyyy");

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = matchedBinTypes,
				};

				binDays.Add(binDay);
			}

			var getBinDaysResponse = new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};

			return getBinDaysResponse;
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}
}
