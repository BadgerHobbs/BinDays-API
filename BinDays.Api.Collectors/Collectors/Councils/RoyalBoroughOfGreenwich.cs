namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Royal Borough of Greenwich.
/// </summary>
internal sealed partial class RoyalBoroughOfGreenwich : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Royal Borough of Greenwich";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.royalgreenwich.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "greenwich";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "General Waste" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Weekly Collection" ],
		},
		new()
		{
			Name = "Food and Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Weekly Collection" ],
		},
	];

	/// <summary>
	/// The metadata key for the collection day.
	/// </summary>
	private const string _collectionDayMetadataKey = "collectionDay";

	/// <summary>
	/// The metadata key for the collection frequency.
	/// </summary>
	private const string _frequencyMetadataKey = "frequency";

	/// <summary>
	/// Regex for the week A and week B table ranges.
	/// </summary>
	[GeneratedRegex(@"<tr><td>\d+<\/td><td>(?<weekA>[^<]+)<\/td><td>(?<weekB>[^<]*)<\/td><\/tr>")]
	private static partial Regex WeekRangeRegex();

	/// <summary>
	/// Regex for extracting date ranges from each week table cell.
	/// </summary>
	[GeneratedRegex(@"Monday\s+(?<startDay>\d{1,2})\s+(?<startMonth>[A-Za-z]+)(?:\s+(?<startYear>\d{4}))?\s+to\s+Friday\s+(?<endDay>\d{1,2})\s+(?<endMonth>[A-Za-z]+)(?:\s+(?<endYear>\d{4}))?")]
	private static partial Regex WeekDateRangeRegex();

	/// <summary>
	/// Regex for normalizing address whitespace.
	/// </summary>
	[GeneratedRegex(@"\s+")]
	private static partial Regex WhitespaceRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for address lookup
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://www.royalgreenwich.gov.uk/site/custom_scripts/apps/waste-collection/source.php?term={postcode}",
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from the response
		else if (clientSideResponse.RequestId == 1)
		{
			using var document = JsonDocument.Parse(clientSideResponse.Content);
			var rawAddresses = document.RootElement.EnumerateArray();

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in rawAddresses)
			{
				var property = WhitespaceRegex().Replace(rawAddress.GetString()!, " ").Trim();

				var address = new Address
				{
					Property = property,
					Postcode = postcode,
					Uid = property,
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
		// Prepare client-side request for the selected address details
		if (clientSideResponse == null)
		{
			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "address", address.Uid! },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://www.royalgreenwich.gov.uk/site/custom_scripts/apps/waste-collection/ajax-response-uprn.php",
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
		// Prepare client-side request for the black-top schedule page
		else if (clientSideResponse.RequestId == 1)
		{
			using var document = JsonDocument.Parse(clientSideResponse.Content);
			var collectionDay = document.RootElement.GetProperty("Day").GetString()!;
			var frequency = document.RootElement.GetProperty("Frequency").GetString()!;

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://www.royalgreenwich.gov.uk/recycling-and-rubbish/bins-and-collections/black-top-bin-collections",
				Method = "GET",
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ _collectionDayMetadataKey, collectionDay },
						{ _frequencyMetadataKey, frequency },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process collection dates from the week schedule table
		else if (clientSideResponse.RequestId == 2)
		{
			var collectionDay = clientSideResponse.Options.Metadata[_collectionDayMetadataKey];
			var frequency = clientSideResponse.Options.Metadata[_frequencyMetadataKey];
			if (!frequency.Equals("Week A", StringComparison.OrdinalIgnoreCase)
				&& !frequency.Equals("Week B", StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException($"Unsupported collection frequency: {frequency}.");
			}

			var rawWeekRanges = WeekRangeRegex().Matches(clientSideResponse.Content)!;

			var weeklyCollectionDates = new HashSet<DateOnly>();
			var generalWasteDates = new HashSet<DateOnly>();
			var currentYear = 0;

			// Iterate through each week range, and calculate collection dates
			foreach (Match rawWeekRange in rawWeekRanges)
			{
				var weekARange = rawWeekRange.Groups["weekA"].Value.Trim();
				var weekACollectionDate = ParseCollectionDateFromRange(weekARange, collectionDay, ref currentYear);

				weeklyCollectionDates.Add(weekACollectionDate);

				if (frequency.Equals("Week A", StringComparison.OrdinalIgnoreCase))
				{
					generalWasteDates.Add(weekACollectionDate);
				}

				var weekBRange = rawWeekRange.Groups["weekB"].Value.Trim();
				if (string.IsNullOrWhiteSpace(weekBRange) || weekBRange == "&nbsp;")
				{
					continue;
				}

				var weekBCollectionDate = ParseCollectionDateFromRange(weekBRange, collectionDay, ref currentYear);
				weeklyCollectionDates.Add(weekBCollectionDate);

				if (frequency.Equals("Week B", StringComparison.OrdinalIgnoreCase))
				{
					generalWasteDates.Add(weekBCollectionDate);
				}
			}

			var weeklyBins = ProcessingUtilities.GetMatchingBins(_binTypes, "Weekly Collection");
			var generalWasteBins = ProcessingUtilities.GetMatchingBins(_binTypes, "General Waste");
			var binDays = new List<BinDay>();

			// Iterate through each weekly collection date, and add recycling and food/garden bins
			foreach (var weeklyCollectionDate in weeklyCollectionDates)
			{
				var binDay = new BinDay
				{
					Date = weeklyCollectionDate,
					Address = address,
					Bins = weeklyBins,
				};

				binDays.Add(binDay);
			}

			// Iterate through each general waste date, and add the general waste bin
			foreach (var generalWasteDate in generalWasteDates)
			{
				var binDay = new BinDay
				{
					Date = generalWasteDate,
					Address = address,
					Bins = generalWasteBins,
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

	/// <summary>
	/// Parses a week date range and returns the collection date for the given collection day.
	/// </summary>
	private static DateOnly ParseCollectionDateFromRange(string weekRange, string collectionDay, ref int currentYear)
	{
		var rangeMatch = WeekDateRangeRegex().Match(weekRange);
		var startDay = rangeMatch.Groups["startDay"].Value;
		var startMonth = rangeMatch.Groups["startMonth"].Value;
		var startYearText = rangeMatch.Groups["startYear"].Value;
		var endMonth = rangeMatch.Groups["endMonth"].Value;
		var endYearText = rangeMatch.Groups["endYear"].Value;

		int startYear;
		if (!string.IsNullOrWhiteSpace(startYearText))
		{
			startYear = int.Parse(startYearText);
		}
		else if (!string.IsNullOrWhiteSpace(endYearText))
		{
			var endYear = int.Parse(endYearText);
			var startMonthNumber = DateUtilities.ParseDateExact($"1 {startMonth} {endYear}", "d MMMM yyyy").Month;
			var endMonthNumber = DateUtilities.ParseDateExact($"1 {endMonth} {endYear}", "d MMMM yyyy").Month;

			startYear = startMonthNumber > endMonthNumber
				? endYear - 1
				: endYear;
		}
		else
		{
			startYear = currentYear;
		}

		int endYearValue;
		if (!string.IsNullOrWhiteSpace(endYearText))
		{
			endYearValue = int.Parse(endYearText);
		}
		else
		{
			var startMonthNumber = DateUtilities.ParseDateExact($"1 {startMonth} {startYear}", "d MMMM yyyy").Month;
			var endMonthNumber = DateUtilities.ParseDateExact($"1 {endMonth} {startYear}", "d MMMM yyyy").Month;

			endYearValue = endMonthNumber < startMonthNumber
				? startYear + 1
				: startYear;
		}

		currentYear = endYearValue;

		var startDate = DateUtilities.ParseDateExact($"{startDay} {startMonth} {startYear}", "d MMMM yyyy");
		var dayOffset = GetCollectionDayOffset(collectionDay);
		var collectionDate = startDate.AddDays(dayOffset);

		return collectionDate;
	}

	/// <summary>
	/// Converts a collection day value to an offset from Monday.
	/// </summary>
	private static int GetCollectionDayOffset(string collectionDay)
	{
		var dayOffset = collectionDay switch
		{
			"Monday" => 0,
			"Tuesday" => 1,
			"Wednesday" => 2,
			"Thursday" => 3,
			"Friday" => 4,
			_ => throw new InvalidOperationException($"Unsupported collection day: {collectionDay}."),
		};

		return dayOffset;
	}
}
