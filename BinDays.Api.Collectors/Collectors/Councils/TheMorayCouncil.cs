namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

/// <summary>
/// Collector implementation for The Moray Council.
/// </summary>
internal sealed partial class TheMorayCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "The Moray Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("http://www.moray.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "moray";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = _generalWasteName,
			Colour = BinColour.Green,
			Type = BinType.Bin,
			Keys = [ "Green bin", "General waste" ],
		},
		new()
		{
			Name = _gardenWasteName,
			Colour = BinColour.Brown,
			Type = BinType.Bin,
			Keys = [ "Brown bin", "Garden" ],
		},
		new()
		{
			Name = _paperAndCardName,
			Colour = BinColour.Blue,
			Type = BinType.Bin,
			Keys = [ "Blue bin", "Paper", "Card" ],
		},
		new()
		{
			Name = _plasticsAndCansName,
			Colour = BinColour.Purple,
			Type = BinType.Bin,
			Keys = [ "Purple bin", "Cans", "Plastic" ],
		},
		new()
		{
			Name = _glassName,
			Colour = BinColour.Orange,
			Type = BinType.Box,
			Keys = [ "Orange box", "Glass" ],
		},
	];

	/// <summary>
	/// Key for storing bin days in metadata.
	/// </summary>
	private const string _binDaysMetadataKey = "binDays";

	/// <summary>
	/// Name for the garden waste bin.
	/// </summary>
	private const string _gardenWasteName = "Garden Waste";

	/// <summary>
	/// Name for the general waste bin.
	/// </summary>
	private const string _generalWasteName = "General Waste";

	/// <summary>
	/// Name for the glass bin.
	/// </summary>
	private const string _glassName = "Glass";

	/// <summary>
	/// Name for the paper and card bin.
	/// </summary>
	private const string _paperAndCardName = "Paper and Card";

	/// <summary>
	/// Name for the plastics and cans bin.
	/// </summary>
	private const string _plasticsAndCansName = "Plastics and Cans";

	/// <summary>
	/// Key for storing remaining calendars in metadata.
	/// </summary>
	private const string _remainingCalendarsMetadataKey = "remainingCalendars";

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
	[GeneratedRegex("href=['\\\"](?<url>(?:https?://bindayfinder\\.moray\\.gov\\.uk/)?cal_(?<year>\\d{4})_view\\.php\\?id=(?<id>\\d+))['\\\"]", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
	private static partial Regex CalendarLinksRegex();

	/// <summary>
	/// Regex for extracting month blocks from the calendar.
	/// </summary>
	[GeneratedRegex("<div class=['\\\"]month-header['\\\"]><h2>(?<month>[^<]+)</h2></div>.*?<div class=['\\\"]days-container['\\\"]>(?<days>.*?)</div>\\s*</div>", RegexOptions.Singleline)]
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
			var formattedPostcode = HttpUtility.UrlEncode(ProcessingUtilities.FormatPostcode(postcode));

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://bindayfinder.moray.gov.uk/refuse_roads.php?strname=&pcode={formattedPostcode}",
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
			var addressMatches = AddressesRegex().Matches(clientSideResponse.Content);
			var addresses = new List<Address>();

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
		// Process bin days from response
		else if (clientSideResponse.RequestId == 1)
		{
			var calendarUrls = CalendarLinksRegex()
				.Matches(clientSideResponse.Content)
				.Select(match => NormalizeCalendarUrl(match.Groups["url"].Value))
				.Distinct()
				.OrderBy(url => url, StringComparer.OrdinalIgnoreCase)
				.ToList();

			if (calendarUrls.Count == 0)
			{
				throw new InvalidOperationException("No calendar links found for the selected address.");
			}

			var remainingCalendars = calendarUrls.Skip(1).ToList();
			var metadata = new Dictionary<string, string>();
			if (remainingCalendars.Count > 0)
			{
				metadata.Add(_remainingCalendarsMetadataKey, string.Join(",", remainingCalendars));
			}

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = calendarUrls.First(),
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
		// Process bin days from response
		else if (clientSideResponse.RequestId >= 2)
		{
			var metadata = clientSideResponse.Options.Metadata;
			var binDays = new List<(DateOnly Date, string Code)>();

			if (metadata.TryGetValue(_binDaysMetadataKey, out var existingBinDays))
			{
				binDays.AddRange(ParseBinDaysMetadata(existingBinDays));
			}

			binDays.AddRange(ParseCalendarContent(clientSideResponse.Content));

			var remainingCalendarMetadata = metadata.GetValueOrDefault(_remainingCalendarsMetadataKey);
			var remainingCalendarUrls = string.IsNullOrWhiteSpace(remainingCalendarMetadata)
				? []
				: remainingCalendarMetadata.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

			if (remainingCalendarUrls.Count > 0)
			{
				var nextCalendarUrl = remainingCalendarUrls.First();
				var nextRemainingCalendars = remainingCalendarUrls.Skip(1).ToList();
				var nextMetadata = new Dictionary<string, string>
				{
					{ _binDaysMetadataKey, SerialiseBinDaysMetadata(binDays) },
				};

				if (nextRemainingCalendars.Count > 0)
				{
					nextMetadata.Add(_remainingCalendarsMetadataKey, string.Join(",", nextRemainingCalendars));
				}

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = clientSideResponse.RequestId + 1,
					Url = nextCalendarUrl,
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

			var binDayResults = binDays.Select(binDay => new BinDay
			{
				Date = binDay.Date,
				Address = address,
				Bins = GetBinsForCode(binDay.Code),
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

	private static string NormalizeCalendarUrl(string calendarUrl)
	{
		if (calendarUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
		{
			return calendarUrl;
		}

		return $"https://bindayfinder.moray.gov.uk/{calendarUrl.TrimStart('/')}";
	}

	private static IReadOnlyCollection<(DateOnly Date, string Code)> ParseCalendarContent(string content)
	{
		var yearMatch = CalendarYearRegex().Match(content);
		if (!yearMatch.Success)
		{
			throw new InvalidOperationException("Calendar year not found in response.");
		}

		var year = int.Parse(yearMatch.Groups["year"].Value, CultureInfo.InvariantCulture);

		var binDays = new List<(DateOnly Date, string Code)>();
		var monthMatches = CalendarMonthRegex().Matches(content);

		foreach (Match monthMatch in monthMatches)
		{
			var monthName = monthMatch.Groups["month"].Value.Trim();
			var monthNumber = DateTime.ParseExact(monthName, "MMMM", CultureInfo.InvariantCulture).Month;
			var daysHtml = monthMatch.Groups["days"].Value;

			foreach (Match dayMatch in CalendarDayRegex().Matches(daysHtml))
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

		return binDays.AsReadOnly();
	}

	private static IReadOnlyCollection<(DateOnly Date, string Code)> ParseBinDaysMetadata(string metadata)
	{
		var binDays = new List<(DateOnly Date, string Code)>();
		var entries = metadata.Split("|", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		foreach (var entry in entries)
		{
			var parts = entry.Split(":", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			if (parts.Length != 2)
			{
				continue;
			}

			var date = DateOnly.ParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture);
			var code = parts[1];

			binDays.Add((date, code));
		}

		return binDays.AsReadOnly();
	}

	private static string SerialiseBinDaysMetadata(IEnumerable<(DateOnly Date, string Code)> binDays)
	{
		return string.Join(
			"|",
			binDays.Select(binDay => $"{binDay.Date:yyyy-MM-dd}:{binDay.Code}")
		);
	}

	private IReadOnlyCollection<Bin> GetBinsForCode(string code)
	{
		var upperCode = code.ToUpperInvariant();

		if (upperCode == "B")
		{
			return _binTypes.Where(bin => bin.Name == _gardenWasteName).ToList().AsReadOnly();
		}
		else if (upperCode == "GPOC")
		{
			return _binTypes.Where(bin => bin.Name != _gardenWasteName).ToList().AsReadOnly();
		}
		else if (upperCode == "GBPOC")
		{
			return _binTypes.ToList().AsReadOnly();
		}

		throw new InvalidOperationException($"Unknown bin code: {code}");
	}
}
