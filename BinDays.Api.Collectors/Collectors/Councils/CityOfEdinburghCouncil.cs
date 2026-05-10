namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for City of Edinburgh Council.
/// </summary>
internal sealed partial class CityOfEdinburghCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "City of Edinburgh Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.edinburgh.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "edinburgh";

	/// <summary>
	/// The grey waste bin, collected fortnightly on the phase indicated by the street's calendar code.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _greyBins =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Grey,
			Keys = [],
		},
	];

	/// <summary>
	/// The recycling bins, collected fortnightly on the opposite phase to the grey bin.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _recyclingBins =
	[
		new()
		{
			Name = "Paper and Card Recycling",
			Colour = BinColour.Blue,
			Keys = [],
		},
		new()
		{
			Name = "Cans, Plastics and Glass Recycling",
			Colour = BinColour.Red,
			Keys = [],
		},
	];

	/// <summary>
	/// Regex to extract latitude from postcodes.io response.
	/// </summary>
	[GeneratedRegex(@"""latitude"":(?<lat>-?[0-9]+\.[0-9]+)")]
	private static partial Regex LatRegex();

	/// <summary>
	/// Regex to extract longitude from postcodes.io response.
	/// </summary>
	[GeneratedRegex(@"""longitude"":(?<lon>-?[0-9]+\.[0-9]+)")]
	private static partial Regex LonRegex();

	/// <summary>
	/// Regex to extract the road name from a Nominatim reverse geocode response.
	/// </summary>
	[GeneratedRegex(@"""road"":""(?<road>[^""]+)""")]
	private static partial Regex RoadRegex();

	/// <summary>
	/// Regex to extract directory record links from the Edinburgh directory search results.
	/// </summary>
	[GeneratedRegex(@"href=""/directory-record/(?<uid>[0-9]+/[^""]+)"">(?<name>[^<]+)</a>")]
	private static partial Regex DirectoryRecordRegex();

	/// <summary>
	/// Regex to extract the calendar code from an Edinburgh directory record page.
	/// </summary>
	[GeneratedRegex(@"(?<code>(?:Mon|Tue|Wed|Thu|Fri)_[12])")]
	private static partial Regex CalendarCodeRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for postcodes.io lookup
		if (clientSideResponse == null)
		{
			var postcodeNoSpace = postcode.Replace(" ", "");

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://api.postcodes.io/postcodes/{postcodeNoSpace}",
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for Nominatim reverse geocode
		else if (clientSideResponse.RequestId == 1)
		{
			var lat = LatRegex().Match(clientSideResponse.Content).Groups["lat"].Value;
			var lon = LonRegex().Match(clientSideResponse.Content).Groups["lon"].Value;

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"https://nominatim.openstreetmap.org/reverse?lat={lat}&lon={lon}&format=json&addressdetails=1&zoom=18",
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
		// Prepare client-side request for Edinburgh directory search
		else if (clientSideResponse.RequestId == 2)
		{
			var road = RoadRegex().Match(clientSideResponse.Content).Groups["road"].Value;

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = "https://www.edinburgh.gov.uk/directory/search",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{ "directoryID", "10251" },
					{ "showInMap", "" },
					{ "keywords", road },
					{ "search", "Search directory" },
				}),
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from directory search results
		else if (clientSideResponse.RequestId == 3)
		{
			var records = DirectoryRecordRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match record in records)
			{
				var address = new Address
				{
					Property = record.Groups["name"].Value.Trim(),
					Postcode = postcode,
					Uid = record.Groups["uid"].Value.Trim(),
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
		// Prepare client-side request for the Edinburgh directory record page
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://www.edinburgh.gov.uk/directory-record/{address.Uid!}",
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from directory record
		else if (clientSideResponse.RequestId == 1)
		{
			var calendarCode = CalendarCodeRegex().Match(clientSideResponse.Content).Groups["code"].Value;

			// Edinburgh alternates grey waste and recycling on opposite fortnightly phases.
			// The calendar code on the directory record corresponds to the grey waste phase.
			var parts = calendarCode.Split('_');
			var day = parts[0];
			var phase = int.Parse(parts[1], CultureInfo.InvariantCulture);
			var oppositeCode = $"{day}_{(phase == 1 ? 2 : 1)}";

			var binDays = new List<BinDay>();

			// Iterate through each grey waste collection date, and create a new bin day object
			foreach (var date in GetCollectionDates(calendarCode))
			{
				binDays.Add(new BinDay
				{
					Date = date,
					Address = address,
					Bins = [.. _greyBins],
				});
			}

			// Iterate through each recycling collection date, and create a new bin day object
			foreach (var date in GetCollectionDates(oppositeCode))
			{
				binDays.Add(new BinDay
				{
					Date = date,
					Address = address,
					Bins = [.. _recyclingBins],
				});
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
	/// Generates fortnightly collection dates for the given calendar code across the current and next year.
	/// Phase 1 falls on even ISO weeks; phase 2 falls on odd ISO weeks.
	/// Grey waste uses the street's own code; recycling uses the opposite phase code.
	/// </summary>
	private static IEnumerable<DateOnly> GetCollectionDates(string calendarCode)
	{
		var parts = calendarCode.Split('_');
		var dayOfWeek = ParseDayOfWeek(parts[0]);
		var phase = int.Parse(parts[1], CultureInfo.InvariantCulture);

		var today = DateOnly.FromDateTime(DateTime.Today);

		foreach (var date in GetDatesForYear(dayOfWeek, phase, today.Year))
		{
			yield return date;
		}

		foreach (var date in GetDatesForYear(dayOfWeek, phase, today.Year + 1))
		{
			yield return date;
		}
	}

	/// <summary>
	/// Returns all fortnightly dates within the given year that match the day of week and phase.
	/// </summary>
	private static IEnumerable<DateOnly> GetDatesForYear(DayOfWeek dayOfWeek, int phase, int year)
	{
		var date = new DateOnly(year, 1, 1);

		while (date.DayOfWeek != dayOfWeek)
		{
			date = date.AddDays(1);
		}

		var isoWeek = ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue));
		var wantEvenWeek = phase == 1;

		if (isoWeek % 2 == 0 != wantEvenWeek)
		{
			date = date.AddDays(7);
		}

		while (date.Year == year)
		{
			yield return date;
			date = date.AddDays(14);
		}
	}

	/// <summary>
	/// Parses a three-letter day abbreviation into a <see cref="DayOfWeek"/>.
	/// </summary>
	private static DayOfWeek ParseDayOfWeek(string abbreviated) => abbreviated switch
	{
		"Mon" => DayOfWeek.Monday,
		"Tue" => DayOfWeek.Tuesday,
		"Wed" => DayOfWeek.Wednesday,
		"Thu" => DayOfWeek.Thursday,
		"Fri" => DayOfWeek.Friday,
		_ => throw new InvalidOperationException($"Unknown day abbreviation: {abbreviated}"),
	};
}
