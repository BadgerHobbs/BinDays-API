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
	/// Regex capturing the Week A and Week B Monday start dates from each schedule row.
	/// Any trailing year is discarded because the day-of-week prefix lets <see cref="DateUtilities.ParseDateInferringYear"/> resolve it.
	/// The Week B group is optional to tolerate rows with an empty Week B cell.
	/// </summary>
	[GeneratedRegex(@"<tr><td>\d+<\/td><td>(?<weekA>Monday\s+\d{1,2}\s+[A-Za-z]+)(?:\s+\d{4})?\s+to\s+Friday[^<]*<\/td><td>(?:(?<weekB>Monday\s+\d{1,2}\s+[A-Za-z]+)(?:\s+\d{4})?\s+to\s+Friday)?[^<]*<\/td><\/tr>")]
	private static partial Regex WeekRowRegex();

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
				Url = $"https://www.royalgreenwich.gov.uk/site/custom_scripts/apps/waste-collection/source.php?term={Uri.EscapeDataString(postcode)}",
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
						{ "collectionDay", collectionDay },
						{ "frequency", frequency },
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
			var collectionDay = clientSideResponse.Options.Metadata["collectionDay"];
			var frequency = clientSideResponse.Options.Metadata["frequency"];

			// "Weekly" addresses have every bin collected every week; "Week A" / "Week B" addresses have the general waste
			// bin collected fortnightly on their assigned week, while recycling and food/garden waste remain weekly.
			var isWeekly = frequency.Equals("Weekly", StringComparison.OrdinalIgnoreCase);
			var isWeekA = frequency.Equals("Week A", StringComparison.OrdinalIgnoreCase);
			var isWeekB = frequency.Equals("Week B", StringComparison.OrdinalIgnoreCase);
			if (!isWeekly && !isWeekA && !isWeekB)
			{
				throw new InvalidOperationException($"Unsupported collection frequency: {frequency}.");
			}

			var dayOffset = collectionDay switch
			{
				"Monday" => 0,
				"Tuesday" => 1,
				"Wednesday" => 2,
				"Thursday" => 3,
				"Friday" => 4,
				_ => throw new InvalidOperationException($"Unsupported collection day: {collectionDay}."),
			};

			var weeklyBins = ProcessingUtilities.GetMatchingBins(_binTypes, "Weekly Collection");
			var generalWasteBins = ProcessingUtilities.GetMatchingBins(_binTypes, "General Waste");

			// The general waste bin is added to the weeks that match the address's frequency (every week for "Weekly",
			// or only the assigned A/B week otherwise).
			var weekABins = isWeekly || isWeekA ? [.. weeklyBins, .. generalWasteBins] : weeklyBins;
			var weekBBins = isWeekly || isWeekB ? [.. weeklyBins, .. generalWasteBins] : weeklyBins;

			var binDays = new List<BinDay>();

			// Iterate through each schedule row, and add bin days for the Week A and Week B Monday-to-Friday ranges
			foreach (Match row in WeekRowRegex().Matches(clientSideResponse.Content)!)
			{
				var weekADate = DateUtilities.ParseDateInferringYear(row.Groups["weekA"].Value, "dddd d MMMM").AddDays(dayOffset);
				binDays.Add(new BinDay
				{
					Date = weekADate,
					Address = address,
					Bins = weekABins,
				});

				if (row.Groups["weekB"].Success)
				{
					var weekBDate = DateUtilities.ParseDateInferringYear(row.Groups["weekB"].Value, "dddd d MMMM").AddDays(dayOffset);
					binDays.Add(new BinDay
					{
						Date = weekBDate,
						Address = address,
						Bins = weekBBins,
					});
				}
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
