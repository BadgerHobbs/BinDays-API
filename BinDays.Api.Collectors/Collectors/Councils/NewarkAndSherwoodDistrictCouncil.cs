namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Newark and Sherwood District Council.
/// </summary>
internal sealed partial class NewarkAndSherwoodDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Newark and Sherwood District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.newark-sherwooddc.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "newark-and-sherwood";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "Green refuse" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Grey,
			Keys = [ "Silver recycling" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Brown garden recycling" ],
		},
		new()
		{
			Name = "Glass Recycling",
			Colour = BinColour.LightBlue,
			Keys = [ "Teal lid glass recycling" ],
		},
	];

	/// <summary>
	/// Regex for the viewstate token values from input fields.
	/// </summary>
	[GeneratedRegex(@"<input[^>]*?(?:name|id)=""__VIEWSTATE""[^>]*?value=""(?<viewStateValue>[^""]*)""[^>]*?/?>")]
	private static partial Regex ViewStateTokenRegex();

	/// <summary>
	/// Regex for the viewstate generator values from input fields.
	/// </summary>
	[GeneratedRegex(@"<input[^>]*?(?:name|id)=""__VIEWSTATEGENERATOR""[^>]*?value=""(?<viewStateGenerator>[^""]*)""[^>]*?/?>")]
	private static partial Regex ViewStateGeneratorRegex();

	/// <summary>
	/// Regex for the event validation values from input fields.
	/// </summary>
	[GeneratedRegex(@"<input[^>]*?(?:name|id)=""__EVENTVALIDATION""[^>]*?value=""(?<eventValidationValue>[^""]*)""[^>]*?/?>")]
	private static partial Regex EventValidationRegex();

	/// <summary>
	/// Regex for the addresses from the link elements.
	/// </summary>
	[GeneratedRegex(@"<a href=""collection\.aspx\?pid=(?<pid>[^""]+)""[^>]*>.*?&nbsp;(?<address>[^<]+)</a>", RegexOptions.Singleline)]
	private static partial Regex AddressesRegex();

	/// <summary>
	/// Regex for the month tables that contain bin days.
	/// </summary>
	[GeneratedRegex(@"<th>(?<month>[A-Za-z]+\s+\d{4})</th>(?<rows>.*?)</table>", RegexOptions.Singleline)]
	private static partial Regex BinDaysTableRegex();

	/// <summary>
	/// Regex for the bin day rows within a month table.
	/// </summary>
	[GeneratedRegex(@"<tr(?<attributes>[^>]*)>\s*<td>\s*(?<content>.*?)\s*</td>\s*</tr>", RegexOptions.Singleline)]
	private static partial Regex BinDayRowRegex();

	/// <summary>
	/// Regex for the service name and day within a bin day row.
	/// </summary>
	[GeneratedRegex(@"(?:&nbsp;|>\s*)(?<service>[^,<]+),\s*(?<day>[^<]+)", RegexOptions.Singleline)]
	private static partial Regex BinDayContentRegex();

	/// <summary>
	/// Regex for removing ordinal suffixes from dates.
	/// </summary>
	[GeneratedRegex(@"(?<=\d)(st|nd|rd|th)")]
	private static partial Regex OrdinalSuffixRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting search tokens
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://app.newark-sherwooddc.gov.uk/bincollection/",
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for getting addresses
		else if (clientSideResponse.RequestId == 1)
		{
			var viewState = ViewStateTokenRegex().Match(clientSideResponse.Content).Groups["viewStateValue"].Value;
			var viewStateGenerator = ViewStateGeneratorRegex().Match(clientSideResponse.Content).Groups["viewStateGenerator"].Value;
			var eventValidation = EventValidationRegex().Match(clientSideResponse.Content).Groups["eventValidationValue"].Value;

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "__LASTFOCUS", string.Empty },
				{ "__EVENTTARGET", "ctl00$MainContent$LinkButtonSearch" },
				{ "__EVENTARGUMENT", string.Empty },
				{ "__VIEWSTATE", viewState },
				{ "__VIEWSTATEGENERATOR", viewStateGenerator },
				{ "__EVENTVALIDATION", eventValidation },
				{ "ctl00$MainContent$TextBoxSearch", postcode },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://app.newark-sherwooddc.gov.uk/bincollection/",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
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
			var rawAddresses = AddressesRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var uid = rawAddress.Groups["pid"].Value;

				var address = new Address
				{
					Property = rawAddress.Groups["address"].Value.Trim(),
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
		// Prepare client-side request for bin calendar
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://app.newark-sherwooddc.gov.uk/bincollection/calendar?pid={address.Uid!}",
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process current calendar and prepare next year's calendar request if available
		else if (clientSideResponse.RequestId == 1)
		{
			var currentEntries = ParseCalendarEntries(clientSideResponse.Content);

			// The calendar only covers November to October, so next year's calendar is needed to continue past October
			if (!clientSideResponse.Content.Contains("HyperLinkNewCalendar", StringComparison.Ordinal))
			{
				var getBinDaysResponse = new GetBinDaysResponse
				{
					BinDays = CreateBinDays(currentEntries, address),
				};

				return getBinDaysResponse;
			}

			// Encode current calendar entries as "date=service;date=service;..." for next request metadata
			var metadataParts = new List<string>();
			foreach (var (date, service) in currentEntries)
			{
				metadataParts.Add($"{date}={service}");
			}

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"https://app.newark-sherwooddc.gov.uk/bincollection/calendar?pid={address.Uid!}&nc=1",
				Method = "GET",
				Options = new ClientSideOptions
				{
					Metadata = new()
					{
						{ "binDays", string.Join(";", metadataParts) },
					},
				},
			};

			var nextGetBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return nextGetBinDaysResponse;
		}
		// Process next year's calendar and combine with current calendar bin days
		else if (clientSideResponse.RequestId == 2)
		{
			// Decode current calendar entries from metadata ("date=service;date=service;...")
			var entries = new List<(string Date, string Service)>();
			foreach (var entry in clientSideResponse.Options.Metadata["binDays"].Split(';', StringSplitOptions.RemoveEmptyEntries))
			{
				var separatorIdx = entry.IndexOf('=');
				entries.Add((entry[..separatorIdx], entry[(separatorIdx + 1)..]));
			}

			entries.AddRange(ParseCalendarEntries(clientSideResponse.Content));

			var getBinDaysResponse = new GetBinDaysResponse
			{
				BinDays = CreateBinDays(entries, address),
			};

			return getBinDaysResponse;
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <summary>
	/// Parses the collection date and service name of each bin day row in a calendar page.
	/// </summary>
	private static List<(string Date, string Service)> ParseCalendarEntries(string content)
	{
		var monthTables = BinDaysTableRegex().Matches(content)!;

		// Iterate through each month table, and collect calendar entries
		var entries = new List<(string Date, string Service)>();
		foreach (Match monthTable in monthTables)
		{
			var month = monthTable.Groups["month"].Value.Trim();
			var rows = BinDayRowRegex().Matches(monthTable.Groups["rows"].Value)!;

			// Iterate through each bin day row, and create a new calendar entry
			foreach (Match row in rows)
			{
				var attributes = row.Groups["attributes"].Value;

				if (attributes.Contains("danger", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				var rowContent = row.Groups["content"].Value;
				var binDayContent = BinDayContentRegex().Match(rowContent);

				var service = binDayContent.Groups["service"].Value.Trim();
				var day = binDayContent.Groups["day"].Value.Trim();

				day = OrdinalSuffixRegex().Replace(day, string.Empty);

				entries.Add(($"{day} {month}", service));
			}
		}

		return entries;
	}

	/// <summary>
	/// Creates the bin days from parsed calendar entries.
	/// </summary>
	private IReadOnlyCollection<BinDay> CreateBinDays(List<(string Date, string Service)> entries, Address address)
	{
		// Iterate through each calendar entry, and create a new bin day object
		var binDays = new List<BinDay>();
		foreach (var (dateString, service) in entries)
		{
			var collectionDate = DateUtilities.ParseDateExact(dateString, "dddd d MMMM yyyy");

			var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

			var binDay = new BinDay
			{
				Date = collectionDate,
				Address = address,
				Bins = matchedBinTypes,
			};

			binDays.Add(binDay);
		}

		return ProcessingUtilities.ProcessBinDays(binDays);
	}
}
