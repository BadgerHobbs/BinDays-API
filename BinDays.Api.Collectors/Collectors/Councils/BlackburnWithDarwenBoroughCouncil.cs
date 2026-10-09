namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Collector implementation for Blackburn with Darwen Borough Council.
/// </summary>
internal sealed class BlackburnWithDarwenBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Blackburn with Darwen Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://mybins.blackburn.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "blackburn-with-darwen";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = new("Burgundy", "#B8253F"),
			Keys = [ "refuse bin" ],
		},
		new()
		{
			Name = "Paper & Cardboard Recycling",
			Colour = BinColour.Blue,
			Keys = [ "paper and cardboard bin" ],
		},
		new()
		{
			Name = "Glass, Tins & Plastics Recycling",
			Colour = BinColour.Grey,
			Keys = [ "glass, tins and plastics bin" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "greenwaste bin" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "Food Waste Caddy" ],
			Type = BinType.Caddy,
		},
	];

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://mybins.blackburn.gov.uk/api/mybins/getproperties?text={postcode}",
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
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in jsonDoc.RootElement.EnumerateArray())
			{
				var address = new Address
				{
					Property = rawAddress.GetProperty("FullAddress").GetString()!.Trim(),
					Postcode = postcode,
					Uid = rawAddress.GetProperty("UPRN").GetString()!,
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
		// The council website requests the calendar a month at a time, starting with the current month
		var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Europe/London"));

		// Prepare client-side request for getting the current month's bin days
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://mybins.blackburn.gov.uk/api/mybins/getbincollectiondays?uprn={address.Uid}&month={today.Month}&year={today.Year}",
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting the next month's bin days
		else if (clientSideResponse.RequestId == 1)
		{
			var nextMonth = today.AddMonths(1);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"https://mybins.blackburn.gov.uk/api/mybins/getbincollectiondays?uprn={address.Uid}&month={nextMonth.Month}&year={nextMonth.Year}",
				Method = "GET",
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "currentMonth", clientSideResponse.Content },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from both responses
		else if (clientSideResponse.RequestId == 2)
		{
			List<BinDay> binDays =
			[
				.. ParseBinDays(clientSideResponse.Options.Metadata["currentMonth"], address),
				.. ParseBinDays(clientSideResponse.Content, address),
			];

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
	/// Parses the bin days from a month's collection days response.
	/// </summary>
	private List<BinDay> ParseBinDays(string content, Address address)
	{
		using var jsonDoc = JsonDocument.Parse(content);
		var rawDays = jsonDoc.RootElement.GetProperty("BinCollectionDays");

		// Iterate through each day of the month, and create a new bin day object for each collection
		var binDays = new List<BinDay>();
		foreach (var rawDay in rawDays.EnumerateArray())
		{
			// Skip days without collections
			if (rawDay.ValueKind == JsonValueKind.Null)
			{
				continue;
			}

			// Iterate through each collection on the day, and create a new bin day object
			foreach (var rawBinDay in rawDay.EnumerateArray())
			{
				var service = rawBinDay.GetProperty("BinType").GetString()!;

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				// Each collection has its own date, and the bin's next scheduled date (which may be in a later month)
				foreach (var dateProperty in new[] { "CollectionDate", "NextScheduledCollectionDate" })
				{
					// Parse the date (e.g. "2026-10-14")
					var date = DateUtilities.ParseDateExact(rawBinDay.GetProperty(dateProperty).GetString()!, "yyyy-MM-dd");

					var binDay = new BinDay
					{
						Date = date,
						Address = address,
						Bins = matchedBinTypes,
					};

					binDays.Add(binDay);
				}
			}
		}

		return binDays;
	}
}
