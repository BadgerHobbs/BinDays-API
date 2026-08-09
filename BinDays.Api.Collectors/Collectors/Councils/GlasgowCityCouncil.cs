namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Glasgow City Council.
/// </summary>
internal sealed partial class GlasgowCityCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Glasgow City Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.glasgow.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "glasgow";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "greenBin.gif" ],
		},
		new()
		{
			Name = "Paper, Card & Cardboard Recycling",
			Colour = BinColour.Blue,
			Keys = [ "blueBin.gif" ],
		},
		new()
		{
			Name = "Plastics, Cans & Cartons Recycling",
			Colour = BinColour.Grey,
			Keys = [ "greyBin.gif" ],
		},
		new()
		{
			Name = "Glass Recycling",
			Colour = BinColour.Purple,
			Keys = [ "purpleBin.gif" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "foodBin.gif" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "brownBin.gif" ],
		},
	];

	/// <summary>
	/// The address search URL.
	/// </summary>
	private const string _addressSearchUrl = "https://onlineservices.glasgow.gov.uk/forms/refuseandrecyclingcalendar/AddressSearch.aspx";

	/// <summary>
	/// Regex for extracting hidden form fields.
	/// </summary>
	[GeneratedRegex(@"<input(?=[^>]*(?:name|id)=[""'](?<name>__VIEWSTATE|__EVENTVALIDATION)[""'])(?=[^>]*value=[""'](?<value>[^""']*)[""'])[^>]*>", RegexOptions.IgnoreCase)]
	private static partial Regex HiddenFieldRegex();

	/// <summary>
	/// Regex for extracting selectable addresses.
	/// </summary>
	[GeneratedRegex(@"href=""javascript:__doPostBack\(&#39;(?<eventTarget>ctl00\$Application\$Addresses\$Select\d+)&#39;,&#39;&#39;\)""[^>]*>Select<\/a><\/td><td>(?<address>[^<]+)<\/td>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for extracting the UPRN from the calendar page.
	/// </summary>
	[GeneratedRegex(@"UPRN=(?<uprn>\d+)")]
	private static partial Regex UprnRegex();

	/// <summary>
	/// Regex for extracting the next month navigation argument from the calendar page.
	/// </summary>
	[GeneratedRegex(@"__doPostBack\('ctl00\$Application\$Calendar','(?<arg>V\d+)'\)[^""]*""[^>]*title=""Go to the next month""")]
	private static partial Regex NextMonthArgRegex();

	/// <summary>
	/// Regex for extracting calendar day cells with title attributes.
	/// </summary>
	[GeneratedRegex(@"<td title=""(?<date>[^""]+)""[^>]*>(?<content>(?:(?!<\/td>).)*)<\/td>", RegexOptions.Singleline)]
	private static partial Regex CalendarCellRegex();

	/// <summary>
	/// Regex for extracting bin image filenames from calendar cell content.
	/// </summary>
	[GeneratedRegex(@"\.\./Images/Bins/(?<image>[^""]+)""")]
	private static partial Regex BinImageInCellRegex();

	/// <summary>
	/// Regex for removing the "today is" prefix from title text.
	/// </summary>
	[GeneratedRegex(@"^today is\s+", RegexOptions.IgnoreCase)]
	private static partial Regex TodayPrefixRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for loading the address search form
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _addressSearchUrl,
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for searching addresses
		else if (clientSideResponse.RequestId == 1)
		{
			var viewState = GetHiddenFieldValue(clientSideResponse.Content, "__VIEWSTATE");
			var eventValidation = GetHiddenFieldValue(clientSideResponse.Content, "__EVENTVALIDATION");

			var clientSideRequest = CreateAddressSearchRequest(2, postcode, viewState, eventValidation);

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
				var address = new Address
				{
					Property = WebUtility.HtmlDecode(rawAddress.Groups["address"].Value).Trim(),
					Postcode = postcode,
					Uid = rawAddress.Groups["eventTarget"].Value.Trim(),
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
		// Prepare client-side request for loading the address search form
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _addressSearchUrl,
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for searching addresses
		else if (clientSideResponse.RequestId == 1)
		{
			var viewState = GetHiddenFieldValue(clientSideResponse.Content, "__VIEWSTATE");
			var eventValidation = GetHiddenFieldValue(clientSideResponse.Content, "__EVENTVALIDATION");

			var clientSideRequest = CreateAddressSearchRequest(2, address.Postcode!, viewState, eventValidation);

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for selecting the address
		else if (clientSideResponse.RequestId == 2)
		{
			var viewState = GetHiddenFieldValue(clientSideResponse.Content, "__VIEWSTATE");
			var eventValidation = GetHiddenFieldValue(clientSideResponse.Content, "__EVENTVALIDATION");

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "__EVENTTARGET", address.Uid! },
				{ "__VIEWSTATE", viewState },
				{ "__EVENTVALIDATION", eventValidation },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = _addressSearchUrl,
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
				},
				Body = requestBody,
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process current month calendar and prepare next month request
		else if (clientSideResponse.RequestId == 3)
		{
			var uprn = UprnRegex().Match(clientSideResponse.Content).Groups["uprn"].Value;
			var nextMonthArg = NextMonthArgRegex().Match(clientSideResponse.Content).Groups["arg"].Value;
			var viewState = GetHiddenFieldValue(clientSideResponse.Content, "__VIEWSTATE");
			var eventValidation = GetHiddenFieldValue(clientSideResponse.Content, "__EVENTVALIDATION");

			var currentMonthEntries = ParseCalendarEntries(clientSideResponse.Content);

			// Encode current month entries as "date=image;date=image;..." for next request metadata
			var metadataParts = new List<string>();
			foreach (var (date, image) in currentMonthEntries)
			{
				metadataParts.Add($"{date}={image}");
			}

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "__EVENTTARGET", "ctl00$Application$Calendar" },
				{ "__EVENTARGUMENT", nextMonthArg },
				{ "__VIEWSTATE", viewState },
				{ "__EVENTVALIDATION", eventValidation },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 4,
				Url = $"https://onlineservices.glasgow.gov.uk/forms/refuseandrecyclingcalendar/CollectionsCalendar.aspx?UPRN={uprn}",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
				},
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata = new()
					{
						{ "binDays", string.Join(";", metadataParts) },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process next month calendar and combine with current month bin days
		else if (clientSideResponse.RequestId == 4)
		{
			// Decode current month entries from metadata ("date=image;date=image;...")
			var previousEntries = new List<(string Date, string Image)>();
			foreach (var entry in clientSideResponse.Options.Metadata["binDays"].Split(';', StringSplitOptions.RemoveEmptyEntries))
			{
				var separatorIdx = entry.IndexOf('=');
				previousEntries.Add((entry[..separatorIdx], entry[(separatorIdx + 1)..]));
			}

			var nextMonthEntries = ParseCalendarEntries(clientSideResponse.Content);

			// Iterate through all entries and create bin day objects
			var binDays = new List<BinDay>();
			foreach (var (dateString, image) in previousEntries)
			{
				var date = DateUtilities.ParseDateExact(dateString, "dddd, dd MMMM yyyy");
				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, image);

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = matchedBins,
				};

				binDays.Add(binDay);
			}

			foreach (var (dateString, image) in nextMonthEntries)
			{
				var date = DateUtilities.ParseDateExact(dateString, "dddd, dd MMMM yyyy");
				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, image);

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = matchedBins,
				};

				binDays.Add(binDay);
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
	/// Creates a client-side request for postcode address search.
	/// </summary>
	private static ClientSideRequest CreateAddressSearchRequest(int requestId, string postcode, string viewState, string eventValidation)
	{
		var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
		{
			{ "__VIEWSTATE", viewState },
			{ "__EVENTVALIDATION", eventValidation },
			{ "ctl00$Application$Addresses$Search", postcode },
			{ "ctl00$Application$Addresses$ImageButton.x", "80" },
			{ "ctl00$Application$Addresses$ImageButton.y", "18" },
		});

		var clientSideRequest = new ClientSideRequest
		{
			RequestId = requestId,
			Url = _addressSearchUrl,
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", Constants.FormUrlEncoded },
			},
			Body = requestBody,
		};

		return clientSideRequest;
	}

	/// <summary>
	/// Gets the value of a hidden field from the HTML.
	/// </summary>
	private static string GetHiddenFieldValue(string content, string fieldName)
	{
		var hiddenFields = HiddenFieldRegex().Matches(content)!;

		// Iterate through each hidden field to find the requested field
		foreach (Match hiddenField in hiddenFields)
		{
			var name = hiddenField.Groups["name"].Value;
			if (name == fieldName)
			{
				return hiddenField.Groups["value"].Value;
			}
		}

		throw new InvalidOperationException($"Hidden field '{fieldName}' was not found.");
	}

	/// <summary>
	/// Parses calendar day entries from the HTML, returning date and bin image filename pairs.
	/// </summary>
	private static IReadOnlyCollection<(string Date, string Image)> ParseCalendarEntries(string html)
	{
		var entries = new List<(string Date, string Image)>();

		// Iterate through each calendar day cell to extract bin collection data
		foreach (Match cellMatch in CalendarCellRegex().Matches(html)!)
		{
			var rawDate = cellMatch.Groups["date"].Value.Trim();
			var content = cellMatch.Groups["content"].Value;
			var imageMatches = BinImageInCellRegex().Matches(content)!;

			if (imageMatches.Count == 0)
			{
				continue;
			}

			var dateString = TodayPrefixRegex().Replace(rawDate, string.Empty);

			// Iterate through each bin image in the cell
			foreach (Match imageMatch in imageMatches)
			{
				entries.Add((dateString, imageMatch.Groups["image"].Value.Trim()));
			}
		}

		return [.. entries];
	}
}
