namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Gedling Borough Council.
/// </summary>
internal sealed partial class GedlingBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Gedling Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.gedling.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "gedling";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "General Collection Service" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling Collection Service" ],
		},
		new()
		{
			Name = "Glass Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Glass Collection Service" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden Collection Service" ],
		},
	];

	/// <summary>
	/// The URL of the bin collections lookup page.
	/// </summary>
	private const string _lookupPageUrl = "https://waste.digital.gedling.gov.uk/w/webpage/bin-collections";

	/// <summary>
	/// The page id of the lookup page.
	/// </summary>
	private const string _pageId = "PAG0000634GBSHB1";

	/// <summary>
	/// The cell id containing the address search form.
	/// </summary>
	private const string _cellId = "PCL0004494GBSHB1";

	/// <summary>
	/// The fragment id of the address selector field.
	/// </summary>
	private const string _addressFieldId = "PCF0016554GBSHB1";

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
	/// Regex for the bin days from the next collections table rows, matching the service and date.
	/// </summary>
	[GeneratedRegex(@"data-fragment_id=""PCF0015053GBSHB1""[^>]*data-current_value=""(?<service>[^""]+)""[\s\S]*?data-fragment_id=""PCF0014965GBSHB1""[^>]*data-current_value=""(?<date>\d{2}/\d{2}/\d{4})""")]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "code_action", "search" },
				{ "code_params", $$"""{"search_item":"{{postcode}}"}""" },
				{ "fragment_action", "handle_event" },
				{ "fragment_id", _addressFieldId },
				{ "fragment_collection_class", "formtable" },
				{ "action_cell_id", _cellId },
				{ "action_page_id", _pageId },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_lookupPageUrl}?widget_action=fragment_action",
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
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var options = jsonDoc.RootElement.GetProperty("response").GetProperty("options");

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var option in options.EnumerateArray())
			{
				var uid = option.GetProperty("id").GetString()!;

				// Exclude the "No Addresses Found" placeholder option
				if (string.IsNullOrEmpty(uid))
				{
					continue;
				}

				var address = new Address
				{
					Property = option.GetProperty("summary_address").GetString()!.Trim(),
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
		// Prepare client-side request for getting the session cookie and tokens
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
		// Prepare client-side request to submit the selected address
		else if (clientSideResponse.RequestId == 1)
		{
			var csrfToken = CsrfTokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;
			var pageToken = WebpageTokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;
			var cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);

			// The form's unique key ("C_...") is generated per page load, but any key is accepted on submission
			var widgetGroupId = "PWG0002410GBSHB1";
			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "form_check", csrfToken },
				{ "submitted_page_id", _pageId },
				{ "submitted_widget_group_id", widgetGroupId },
				{ $"payload[{_pageId}][{widgetGroupId}][{_cellId}][formtable][C_0][{_addressFieldId}]", address.Uid! },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
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
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request to load the collections page the submission redirects to
		else if (clientSideResponse.RequestId == 2)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var redirectUrl = jsonDoc.RootElement.GetProperty("redirect_url").GetString()!;

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"https://waste.digital.gedling.gov.uk{redirectUrl}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "x-requested-with", Constants.XmlHttpRequest },
				},
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
