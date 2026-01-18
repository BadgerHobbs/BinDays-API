namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for The Moray Council.
/// </summary>
internal sealed partial class TheMorayCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "The Moray Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.moray.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "moray";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Type = BinType.Bin,
			Keys = [ "G" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Type = BinType.Bin,
			Keys = [ "B" ],
		},
		new()
		{
			Name = "Paper and Card",
			Colour = BinColour.Blue,
			Type = BinType.Bin,
			Keys = [ "P" ],
		},
		new()
		{
			Name = "Plastics and Cans",
			Colour = BinColour.Purple,
			Type = BinType.Bin,
			Keys = [ "C" ],
		},
		new()
		{
			Name = "Glass",
			Colour = BinColour.Orange,
			Type = BinType.Box,
			Keys = [ "O" ],
		},
	];

	/// <summary>
	/// Regex for extracting addresses from the results page.
	/// </summary>
	[GeneratedRegex("<a href=\"disp_bins\\.php\\?id=(?<id>\\d+)\">(?<address>.*?)</a>", RegexOptions.Singleline)]
	private static partial Regex AddressesRegex();

	/// <summary>
	/// Regex for extracting day entries from the calendar month.
	/// </summary>
	[GeneratedRegex("<div class=['\\\"](?<class>[^\\\"']*)['\\\"]>(?<day>[^<]+)</div>")]
	private static partial Regex CalendarDayRegex();

	/// <summary>
	/// Regex for extracting calendar links.
	/// </summary>
	[GeneratedRegex(
		"href=['\\\"](?:https?://bindayfinder\\.moray\\.gov\\.uk/)?(?<url>cal_(?<year>\\d{4})_view\\.php\\?id=(?<id>\\d+))['\\\"]",
		RegexOptions.IgnoreCase | RegexOptions.Singleline
	)]
	private static partial Regex CalendarLinksRegex();

	/// <summary>
	/// Regex for extracting month blocks from the calendar.
	/// </summary>
	[GeneratedRegex(
		"<div class=['\\\"]month-header['\\\"]><h2>(?<month>[^<]+)</h2></div>.*?<div class=['\\\"]days-container['\\\"]>(?<days>.*?)</div>\\s*</div>",
		RegexOptions.Singleline
	)]
	private static partial Regex CalendarMonthRegex();

	/// <summary>
	/// Regex for extracting the calendar year.
	/// </summary>
	[GeneratedRegex("Collections for (?<year>\\d{4})")]
	private static partial Regex CalendarYearRegex();

	/// <summary>
	/// Regex for matching whitespace.
	/// </summary>
	[GeneratedRegex("\\s+")]
	private static partial Regex WhitespaceRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://bindayfinder.moray.gov.uk/refuse_roads.php?strname=&pcode={postcode}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
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
			var addressMatches = AddressesRegex().Matches(clientSideResponse.Content)!;
			var addresses = new List<Address>();

			// Iterate through each address, and create a new address object
			foreach (Match addressMatch in addressMatches)
			{
				var property = WhitespaceRegex().Replace(addressMatch.Groups["address"].Value, " ").Trim();

				var address = new Address
				{
					Property = property,
					Postcode = postcode,
					Uid = addressMatch.Groups["id"].Value,
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
				Url = $"https://bindayfinder.moray.gov.uk/disp_bins.php?id={address.Uid}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Extract calendar links and prepare first calendar request
		else if (clientSideResponse.RequestId == 1)
		{
			var calendarMatches = CalendarLinksRegex().Matches(clientSideResponse.Content)!;
			var calendarUrls = calendarMatches
				.Select(match => $"https://bindayfinder.moray.gov.uk/{match.Groups["url"].Value}")
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Order(StringComparer.OrdinalIgnoreCase)
				.Take(3)
				.ToList();

			if (calendarUrls.Count == 0)
			{
				throw new InvalidOperationException("No calendar links found for the selected address.");
			}

			var metadata = new Dictionary<string, string>
			{
				{ "binDays", string.Empty },
				{
					"remainingCalendars",
					calendarUrls.Count > 1 ? string.Join(",", calendarUrls.Skip(1)) : string.Empty
				},
			};

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = calendarUrls[0],
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
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
		// Parse calendar and either request next calendar or return bin days
		else if (clientSideResponse.RequestId >= 2)
		{
			var metadata = clientSideResponse.Options.Metadata;
			var binDays = new List<(DateOnly Date, string Code)>();

			// Parse any existing bin days from metadata
			var entries = metadata["binDays"].Split(
				"|",
				StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
			);

			foreach (var entry in entries)
			{
				var parts = entry.Split(
					":",
					StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
				);
				var date = DateOnly.ParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture);
				var code = parts[1];

				binDays.Add((date, code));
			}

			// Parse current calendar content
			var yearMatch = CalendarYearRegex().Match(clientSideResponse.Content);
			if (!yearMatch.Success)
			{
				throw new InvalidOperationException("Calendar year not found in response.");
			}

			var year = int.Parse(yearMatch.Groups["year"].Value, CultureInfo.InvariantCulture);
			var monthMatches = CalendarMonthRegex().Matches(clientSideResponse.Content)!;

			foreach (Match monthMatch in monthMatches)
			{
				var monthName = monthMatch.Groups["month"].Value.Trim();
				var monthNumber = DateTime.ParseExact(monthName, "MMMM", CultureInfo.InvariantCulture).Month;
				var daysHtml = monthMatch.Groups["days"].Value;
				var dayMatches = CalendarDayRegex().Matches(daysHtml)!;

				foreach (Match dayMatch in dayMatches)
				{
					var className = dayMatch.Groups["class"].Value.Trim();
					var dayText = dayMatch.Groups["day"].Value.Trim();

					if (string.IsNullOrWhiteSpace(dayText) ||
						string.IsNullOrWhiteSpace(className) ||
						string.Equals(className, "blank", StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					var date = DateOnly.ParseExact(
						$"{dayText}-{monthNumber}-{year}",
						"d-M-yyyy",
						CultureInfo.InvariantCulture
					);

					binDays.Add((date, className));
				}
			}

			// Check if there are more calendars to process
			var remainingCalendars = metadata["remainingCalendars"]
				.Split(
					",",
					StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
				)
				.ToList();

			if (remainingCalendars.Count > 0)
			{
				var nextMetadata = new Dictionary<string, string>
				{
					{
						"binDays",
						string.Join("|", binDays.Select(bd => $"{bd.Date:yyyy-MM-dd}:{bd.Code}"))
					},
					{
						"remainingCalendars",
						remainingCalendars.Count > 1 ? string.Join(",", remainingCalendars.Skip(1)) : string.Empty
					},
				};

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = clientSideResponse.RequestId + 1,
					Url = remainingCalendars[0],
					Method = "GET",
					Headers = new()
					{
						{ "user-agent", Constants.UserAgent },
					},
					Options = new ClientSideOptions
					{
						Metadata = nextMetadata,
					},
				};

				var getBinDaysResponse = new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest,
				};

				return getBinDaysResponse;
			}

			// All calendars processed, return bin days
			var binDayResults = binDays.Select(binDay =>
			{
				var bins = binDay.Code.ToCharArray()
					.SelectMany(c => _binTypes.Where(bin => bin.Keys.Any(key =>
						string.Equals(key, c.ToString(), StringComparison.OrdinalIgnoreCase)
					)))
					.Distinct()
					.ToList();

				return new BinDay
				{
					Date = binDay.Date,
					Address = address,
					Bins = bins,
				};
			}).ToList();

			var getBinDaysResponseFinal = new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDayResults),
			};

			return getBinDaysResponseFinal;
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}
}
