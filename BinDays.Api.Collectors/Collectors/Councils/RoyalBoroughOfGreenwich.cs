namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
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
			Keys = [ "black top" ],
		},
		new()
		{
			Name = "Paper, Card and Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "blue top" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "green top" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "green top" ],
		},
	];

	/// <summary>
	/// Regex for the schedule year on the black top collections page.
	/// </summary>
	[GeneratedRegex(@"Black top bin collections for (?<year>\d{4})")]
	private static partial Regex ScheduleYearRegex();

	/// <summary>
	/// Regex for the week A and week B date ranges in the black top collection table.
	/// </summary>
	[GeneratedRegex(@"<tr>\s*<td>\d+<\/td>\s*<td>(?<weekA>[^<]+)<\/td>\s*<td>(?<weekB>[^<]*)<\/td>\s*<\/tr>", RegexOptions.Singleline)]
	private static partial Regex WeekRowRegex();

	/// <summary>
	/// Regex for parsing a collection week range.
	/// </summary>
	[GeneratedRegex(@"^(?<startDayName>[A-Za-z]+)\s+(?<startDay>\d{1,2})\s+(?<startMonth>[A-Za-z]+)(?:\s+(?<startYear>\d{4}))?\s+to\s+(?<endDayName>[A-Za-z]+)\s+(?<endDay>\d{1,2})\s+(?<endMonth>[A-Za-z]+)(?:\s+(?<endYear>\d{4}))?$")]
	private static partial Regex WeekRangeRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
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
		// Process addresses from response
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);
			var rawAddresses = jsonDocument.RootElement.EnumerateArray();

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in rawAddresses)
			{
				var selectedAddress = rawAddress.GetString()!;

				var address = new Address
				{
					Property = selectedAddress,
					Postcode = postcode,
					Uid = selectedAddress,
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
		// Prepare client-side request for getting collection pattern
		if (clientSideResponse == null)
		{
			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(
				new()
				{
					{ "address", address.Uid! },
				}
			);

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
		// Prepare client-side request for getting black top collection schedule
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);
			var collectionDay = jsonDocument.RootElement.GetProperty("Day").GetString()!;
			var collectionFrequency = jsonDocument.RootElement.GetProperty("Frequency").GetString()!;

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://www.royalgreenwich.gov.uk/recycling-and-rubbish/bins-and-collections/black-top-bin-collections",
				Method = "GET",
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "collectionDay", collectionDay },
						{ "collectionFrequency", collectionFrequency },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 2)
		{
			var collectionDay = clientSideResponse.Options.Metadata["collectionDay"];
			var collectionFrequency = clientSideResponse.Options.Metadata["collectionFrequency"];

			var scheduleYearMatch = ScheduleYearRegex().Match(clientSideResponse.Content);
			var scheduleYear = int.Parse(scheduleYearMatch.Groups["year"].Value, CultureInfo.InvariantCulture);

			var collectionDayOffset = collectionDay switch
			{
				"Monday" => 0,
				"Tuesday" => 1,
				"Wednesday" => 2,
				"Thursday" => 3,
				"Friday" => 4,
				"Saturday" => 5,
				"Sunday" => 6,
				_ => throw new InvalidOperationException($"Unsupported collection day: {collectionDay}"),
			};

			var weekRows = WeekRowRegex().Matches(clientSideResponse.Content)!;

			var weeklyCollectionDates = new HashSet<DateOnly>();
			var generalWasteDates = new HashSet<DateOnly>();

			// Iterate through each week row, and create date entries for weekly and fortnightly collections
			foreach (Match weekRow in weekRows)
			{
				var weekA = WebUtility.HtmlDecode(weekRow.Groups["weekA"].Value).Trim();
				var weekB = WebUtility.HtmlDecode(weekRow.Groups["weekB"].Value).Trim();

				if (weekA.Contains("to", StringComparison.OrdinalIgnoreCase))
				{
					var weekAStartDate = ParseWeekStartDate(weekA, scheduleYear);
					var weekACollectionDate = weekAStartDate.AddDays(collectionDayOffset);
					weeklyCollectionDates.Add(weekACollectionDate);

					if (collectionFrequency.Equals("Week A", StringComparison.OrdinalIgnoreCase)
						|| collectionFrequency.Equals("Weekly", StringComparison.OrdinalIgnoreCase))
					{
						generalWasteDates.Add(weekACollectionDate);
					}
				}

				if (weekB.Contains("to", StringComparison.OrdinalIgnoreCase))
				{
					var weekBStartDate = ParseWeekStartDate(weekB, scheduleYear);
					var weekBCollectionDate = weekBStartDate.AddDays(collectionDayOffset);
					weeklyCollectionDates.Add(weekBCollectionDate);

					if (collectionFrequency.Equals("Week B", StringComparison.OrdinalIgnoreCase)
						|| collectionFrequency.Equals("Weekly", StringComparison.OrdinalIgnoreCase))
					{
						generalWasteDates.Add(weekBCollectionDate);
					}
				}
			}

			var weeklyBins = ProcessingUtilities.GetMatchingBins(_binTypes, "blue top green top");
			var generalWasteBins = ProcessingUtilities.GetMatchingBins(_binTypes, "black top");

			var binDays = new List<BinDay>();

			// Iterate through each weekly collection date, and create a new bin day object
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

			// Iterate through each black top collection date, and create a new bin day object
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
	/// Parses a collection week range and returns its start date.
	/// </summary>
	private static DateOnly ParseWeekStartDate(string weekRange, int defaultYear)
	{
		var weekRangeMatch = WeekRangeRegex().Match(weekRange);
		var startDay = weekRangeMatch.Groups["startDay"].Value;
		var startMonth = weekRangeMatch.Groups["startMonth"].Value;
		var startYear = weekRangeMatch.Groups["startYear"].Value;

		var year = string.IsNullOrWhiteSpace(startYear)
			? defaultYear
			: int.Parse(startYear, CultureInfo.InvariantCulture);

		var weekStartDate = DateUtilities.ParseDateExact($"{startDay} {startMonth} {year}", "d MMMM yyyy");
		return weekStartDate;
	}
}
