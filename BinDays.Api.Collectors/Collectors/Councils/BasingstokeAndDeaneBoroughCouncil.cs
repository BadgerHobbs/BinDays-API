namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Basingstoke and Deane Borough Council.
/// </summary>
internal sealed partial class BasingstokeAndDeaneBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Basingstoke and Deane Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.basingstoke.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "basingstoke-and-deane";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Waste", ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling", ],
		},
		new()
		{
			Name = "Glass Recycling",
			Colour = BinColour.Green,
			Keys = [ "Glass", ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Food waste", ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden", ],
		},
	];

	private const string _binCollectionsUrl = "https://www.basingstoke.gov.uk/bincollections";
	private const string _formActionUrl = "https://www.basingstoke.gov.uk/rte.aspx?id=1270";
	private const string _scriptManagerPrefix = "rteelem$ctl03$ctl01";
	private const string _searchEventTarget = "rteelem$ctl03$gapAddress$ctl04";
	private const string _selectEventTarget = "rteelem$ctl03$gapAddress$ctl07";
	private const string _origin = "https://www.basingstoke.gov.uk";

	/// <summary>
	/// Regex for parsing hidden fields from AJAX responses.
	/// </summary>
	[GeneratedRegex(@"hiddenField\|(?<name>__VIEWSTATEGENERATOR|__EVENTVALIDATION|__VIEWSTATE)\|(?<value>[^|]+)")]
	private static partial Regex AjaxHiddenFieldRegex();

	/// <summary>
	/// Regex for parsing hidden fields from HTML responses.
	/// </summary>
	[GeneratedRegex(@"name=""(?<name>__VIEWSTATEGENERATOR|__EVENTVALIDATION|__VIEWSTATE)""[^>]+value=""(?<value>[^""]+)""")]
	private static partial Regex HtmlHiddenFieldRegex();

	/// <summary>
	/// Regex for parsing address options.
	/// </summary>
	[GeneratedRegex(@"<option value=""(?<uid>[^""]+)"">(?<address>[^<]+)</option>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for parsing service blocks from the response.
	/// </summary>
	[GeneratedRegex(@"<div[^>]*class=""service""[^>]*>(?<service>.*?)(?=(?:<div[^>]*class=""service""[^>]*>|$))", RegexOptions.Singleline)]
	private static partial Regex ServiceBlockRegex();

	/// <summary>
	/// Regex for parsing headings from the service blocks.
	/// </summary>
	[GeneratedRegex(@"<h2>(?<heading>[^<]+)</h2>", RegexOptions.Singleline)]
	private static partial Regex HeadingRegex();

	/// <summary>
	/// Regex for parsing collection dates.
	/// </summary>
	[GeneratedRegex(@"<li>(?<date>[^<]+)</li>")]
	private static partial Regex DateRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for initial page load
		if (clientSideResponse == null)
		{
			var clientSideRequest = CreateInitialRequest();

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for postcode search
		else if (clientSideResponse.RequestId == 1)
		{
			var clientSideRequest = CreatePostcodeSearchRequest(clientSideResponse.Content, postcode, 2);

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 2)
		{
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var uid = rawAddress.Groups["uid"].Value;

				if (string.IsNullOrWhiteSpace(uid))
				{
					continue;
				}

				var address = new Address
				{
					Property = WebUtility.HtmlDecode(rawAddress.Groups["address"].Value).Trim(),
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

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for initial page load
		if (clientSideResponse == null)
		{
			var clientSideRequest = CreateInitialRequest();

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for postcode search
		else if (clientSideResponse.RequestId == 1)
		{
			var clientSideRequest = CreatePostcodeSearchRequest(clientSideResponse.Content, address.Postcode!, 2);

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for selecting the address
		else if (clientSideResponse.RequestId == 2)
		{
			var viewState = GetHiddenField(clientSideResponse.Content, "__VIEWSTATE");
			var viewStateGenerator = GetHiddenField(clientSideResponse.Content, "__VIEWSTATEGENERATOR");
			var eventValidation = GetHiddenField(clientSideResponse.Content, "__EVENTVALIDATION");

			Dictionary<string, string> formData = new()
			{
				{ "rteelem$ctl03$ctl00", $"{_scriptManagerPrefix}|{_selectEventTarget}" },
				{ "rteelem$ctl03$gapAddress$lstStage2_SearchResults", address.Uid! },
				{ "__EVENTTARGET", _selectEventTarget },
				{ "__EVENTARGUMENT", string.Empty },
				{ "__VIEWSTATE", viewState },
				{ "__VIEWSTATEGENERATOR", viewStateGenerator },
				{ "__EVENTVALIDATION", eventValidation },
				{ "__ASYNCPOST", "true" },
			};

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = _formActionUrl,
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "x-requested-with", "XMLHttpRequest" },
					{ "x-microsoftajax", "Delta=true" },
					{ "content-type", "application/x-www-form-urlencoded; charset=utf-8" },
					{ "origin", _origin },
					{ "referer", _binCollectionsUrl },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(formData),
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
			var rawServices = ServiceBlockRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each service, and create bin day objects
			var binDays = new List<BinDay>();
			foreach (Match rawService in rawServices)
			{
				var serviceContent = rawService.Groups["service"].Value;
				var heading = HeadingRegex().Match(serviceContent).Groups["heading"].Value.Trim();

				var rawDates = DateRegex().Matches(serviceContent)!;

				if (rawDates.Count == 0)
				{
					continue;
				}

				var serviceName = heading.Replace(" collection dates", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, serviceName);

				// Iterate through each date, and create a new bin day object
				foreach (Match rawDate in rawDates)
				{
					var dateText = WebUtility.HtmlDecode(rawDate.Groups["date"].Value).Trim();

					var date = DateOnly.ParseExact(
						dateText,
						"dddd, dd MMMM yyyy",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);

					var binDay = new BinDay
					{
						Date = date,
						Address = address,
						Bins = matchedBins,
					};

					binDays.Add(binDay);
				}
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
	/// Creates the initial client-side request to load the form.
	/// </summary>
	private static ClientSideRequest CreateInitialRequest()
	{
		return new ClientSideRequest
		{
			RequestId = 1,
			Url = _binCollectionsUrl,
			Method = "GET",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
			},
		};
	}

	/// <summary>
	/// Creates the client-side request for postcode search.
	/// </summary>
	private static ClientSideRequest CreatePostcodeSearchRequest(string content, string postcode, int requestId)
	{
		var viewState = GetHiddenField(content, "__VIEWSTATE");
		var viewStateGenerator = GetHiddenField(content, "__VIEWSTATEGENERATOR");
		var eventValidation = GetHiddenField(content, "__EVENTVALIDATION");

		Dictionary<string, string> formData = new()
		{
			{ "rteelem$ctl03$ctl00", $"{_scriptManagerPrefix}|{_searchEventTarget}" },
			{ "__EVENTTARGET", _searchEventTarget },
			{ "__EVENTARGUMENT", string.Empty },
			{ "__VIEWSTATE", viewState },
			{ "__VIEWSTATEGENERATOR", viewStateGenerator },
			{ "__EVENTVALIDATION", eventValidation },
			{ "rteelem$ctl03$gapAddress$txtStage1_SearchValue", postcode },
			{ "__ASYNCPOST", "true" },
		};

		var clientSideRequest = new ClientSideRequest
		{
			RequestId = requestId,
			Url = _formActionUrl,
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "x-requested-with", "XMLHttpRequest" },
				{ "x-microsoftajax", "Delta=true" },
				{ "content-type", "application/x-www-form-urlencoded; charset=utf-8" },
				{ "origin", _origin },
				{ "referer", _binCollectionsUrl },
			},
			Body = ProcessingUtilities.ConvertDictionaryToFormData(formData),
		};

		return clientSideRequest;
	}

	/// <summary>
	/// Extracts hidden field values from HTML or AJAX responses.
	/// </summary>
	private static string GetHiddenField(string content, string fieldName)
	{
		var ajaxMatches = AjaxHiddenFieldRegex().Matches(content)!;

		foreach (Match ajaxMatch in ajaxMatches)
		{
			if (ajaxMatch.Groups["name"].Value == fieldName)
			{
				return ajaxMatch.Groups["value"].Value;
			}
		}

		var htmlMatches = HtmlHiddenFieldRegex().Matches(content)!;

		foreach (Match htmlMatch in htmlMatches)
		{
			if (htmlMatch.Groups["name"].Value == fieldName)
			{
				return htmlMatch.Groups["value"].Value;
			}
		}

		throw new InvalidOperationException($"Hidden field '{fieldName}' not found in response content.");
	}
}
