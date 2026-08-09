namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Exceptions;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
			Name = "Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Food and Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Food and Garden Waste" ],
		},
	];

	/// <summary>
	/// Regex for the Week A and Week B start dates of each row in the black top bin collection calendar.
	/// The Week B cell is empty (&amp;nbsp;) for the final row of the year, so it is matched as optional.
	/// </summary>
	[GeneratedRegex(@"<tr><td>\d+</td><td>Monday (?<weekA>\d{1,2} \w+(?: \d{4})?) to [^<]*</td><td>(?:Monday (?<weekB>\d{1,2} \w+(?: \d{4})?) to [^<]*|&nbsp;)</td></tr>")]
	private static partial Regex WeekRowRegex();

	/// <summary>
	/// Regex for a trailing year, which the calendar states on only a handful of its cells.
	/// </summary>
	[GeneratedRegex(@"\d{4}$")]
	private static partial Regex YearSuffixRegex();

	/// <summary>
	/// Regex for the usual and revised collection dates of each row in the bank holiday table.
	/// The holiday being described changes through the year, so only the dates are matched.
	/// </summary>
	[GeneratedRegex(@"<tr><td>\w+ (?<usual>\d{1,2} \w+)[^<]*</td><td>\w+ (?<revised>\d{1,2} \w+)[^<]*</td></tr>")]
	private static partial Regex BankHolidayRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting matching addresses
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

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in jsonDocument.RootElement.EnumerateArray())
			{
				var fullAddress = rawAddress.GetString()!.Trim();

				var address = new Address
				{
					Property = fullAddress,
					Postcode = postcode,
					Uid = fullAddress,
				};

				addresses.Add(address);
			}

			if (addresses.Count == 0)
			{
				throw new AddressesNotFoundException(GovUkId, postcode);
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
		// Prepare client-side request for getting the collection day and week for the address
		if (clientSideResponse == null)
		{
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
				Body = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{ "address", address.Uid! },
				}),
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting the bank holiday collection changes
		else if (clientSideResponse.RequestId == 1)
		{
			// Only houses have a published schedule, so flats and commercial premises are not found
			if (clientSideResponse.Content.Contains("ADDRESS_NOT_FOUND", StringComparison.Ordinal))
			{
				throw new BinDaysNotFoundException(GovUkId, address.Postcode!, address.Uid!);
			}

			using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://www.royalgreenwich.gov.uk/recycling-and-rubbish/bins-and-collections/bank-holiday-collection-dates",
				Method = "GET",
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "day", jsonDocument.RootElement.GetProperty("Day").GetString()! },
						{ "frequency", jsonDocument.RootElement.GetProperty("Frequency").GetString()! },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting the black top bin collection calendar
		else if (clientSideResponse.RequestId == 2)
		{
			var rawBankHolidays = BankHolidayRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each changed collection, and pair its usual date with its revised date
			var bankHolidays = new List<string>();
			foreach (Match rawBankHoliday in rawBankHolidays)
			{
				bankHolidays.Add($"{rawBankHoliday.Groups["usual"].Value}>{rawBankHoliday.Groups["revised"].Value}");
			}

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = "https://www.royalgreenwich.gov.uk/recycling-and-rubbish/bins-and-collections/black-top-bin-collections",
				Method = "GET",
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "day", clientSideResponse.Options.Metadata["day"] },
						{ "frequency", clientSideResponse.Options.Metadata["frequency"] },
						{ "bankHolidays", string.Join(';', bankHolidays) },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from the collection calendar
		else if (clientSideResponse.RequestId == 3)
		{
			var dayOfWeek = Enum.Parse<DayOfWeek>(clientSideResponse.Options.Metadata["day"]);
			var frequency = clientSideResponse.Options.Metadata["frequency"];

			// Collection weeks run from the Monday, so Sunday closes a week rather than opening it
			var dayOffset = ((int)dayOfWeek - (int)DayOfWeek.Monday + 7) % 7;

			// The black top (general waste) bin is collected on the address's week of the alternating
			// pair, whereas the recycling and food and garden waste bins are collected every week.
			var isWeekAAddress = frequency switch
			{
				"Week A" => true,
				"Week B" => false,
				_ => throw new InvalidOperationException($"Unknown collection frequency: {frequency}."),
			};

			// Bank holiday changes are published without a year, so they are matched on day and month
			var bankHolidays = new Dictionary<string, string>();
			foreach (var bankHoliday in clientSideResponse.Options.Metadata["bankHolidays"].Split(';', StringSplitOptions.RemoveEmptyEntries))
			{
				var bankHolidayParts = bankHoliday.Split('>', 2);
				bankHolidays[bankHolidayParts[0]] = bankHolidayParts[1];
			}

			var generalWasteBin = _binTypes.Single(bin => bin.Name == "General Waste");
			var weeklyBins = _binTypes.Where(bin => bin.Name != "General Waste");

			var rawWeeks = WeekRowRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each row, and collect its two week cells in calendar order
			var rawWeekStarts = new List<(string Start, bool IsWeekA)>();
			foreach (Match rawWeek in rawWeeks)
			{
				rawWeekStarts.Add((rawWeek.Groups["weekA"].Value, true));

				// The final row of the year has no Week B collection listed
				if (rawWeek.Groups["weekB"].Success)
				{
					rawWeekStarts.Add((rawWeek.Groups["weekB"].Value, false));
				}
			}

			// Iterate through each week, and create a bin day for the collections it contains
			var binDays = new List<BinDay>();
			var year = 0;
			var previousMonday = DateOnly.MinValue;
			foreach (var (rawWeekStart, isWeekA) in rawWeekStarts)
			{
				// Only the first cell of each year states it, so the year is carried forward
				// across the cells that omit it and rolled over when the months wrap around.
				var hasYear = YearSuffixRegex().IsMatch(rawWeekStart);
				var monday = DateUtilities.ParseDateExact(hasYear ? rawWeekStart : $"{rawWeekStart} {year}", "d MMMM yyyy");

				if (!hasYear && monday < previousMonday)
				{
					monday = DateUtilities.ParseDateExact($"{rawWeekStart} {year + 1}", "d MMMM yyyy");
				}

				year = monday.Year;
				previousMonday = monday;

				// Collections changed by a bank holiday are moved to the date the council published
				var collectionDate = monday.AddDays(dayOffset);
				if (bankHolidays.TryGetValue(collectionDate.ToString("d MMMM", CultureInfo.InvariantCulture), out var revised))
				{
					collectionDate = DateUtilities.ParseDateExact($"{revised} {collectionDate.Year}", "d MMMM yyyy");

					// A collection moved out of December lands in the following year
					if (collectionDate < monday)
					{
						collectionDate = collectionDate.AddYears(1);
					}
				}

				var bins = new List<Bin>(weeklyBins);
				if (isWeekA == isWeekAAddress)
				{
					bins.Add(generalWasteBin);
				}

				binDays.Add(new BinDay
				{
					Date = collectionDate,
					Address = address,
					Bins = [.. bins],
				});
			}

			if (binDays.Count == 0)
			{
				throw new BinDaysNotFoundException(GovUkId, address.Postcode!, address.Uid!);
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
