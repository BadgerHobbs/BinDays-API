namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Fareham Borough Council.
/// </summary>
internal sealed partial class FarehamBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Fareham Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.fareham.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "fareham";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "Refuse" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling" ],
		},
		// Food waste is collected weekly, on the same day as the refuse or the recycling
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Orange,
			Keys =
			[
				"Refuse",
				"Recycling",
			],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden" ],
		},
	];

	/// <summary>
	/// Regex for the addresses and collection details from the CSV data.
	/// </summary>
	[GeneratedRegex(@"^""(?<address>[^""]*)"",""(?<collections>[^""]*)"",""[^""]*"",""(?<garden>[^""]*)"","".*?ref=(?<uid>\d+)", RegexOptions.Multiline)]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the refuse and recycling dates from the collection details.
	/// </summary>
	[GeneratedRegex(@"(?<date>today|tomorrow|\d{2}/\d{2}/\d{4}) \((?<service>[^)]+)\)")]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = CreateSearchRequest(postcode);

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 1)
		{
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var address = new Address
				{
					Property = rawAddress.Groups["address"].Value.Trim(),
					Postcode = postcode,
					Uid = rawAddress.Groups["uid"].Value,
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
		// The postcode search is the only source of current dates, so it is repeated and matched by UPRN
		if (clientSideResponse == null)
		{
			var clientSideRequest = CreateSearchRequest(address.Postcode!);

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 1)
		{
			var rawAddress = AddressRegex().Matches(clientSideResponse.Content)!
				.First(match => match.Groups["uid"].Value == address.Uid);

			var rawBinDays = BinDaysRegex().Matches(rawAddress.Groups["collections"].Value)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["service"].Value;
				var date = DateUtilities.ParseRelativeDateOrExact(rawBinDay.Groups["date"].Value, "dd/MM/yyyy");

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = matchedBinTypes,
				};

				binDays.Add(binDay);
			}

			// The garden waste date has no service name, and is blank for some properties
			var gardenDate = rawAddress.Groups["garden"].Value.Trim();
			if (!string.IsNullOrEmpty(gardenDate))
			{
				var binDay = new BinDay
				{
					Date = DateUtilities.ParseDateExact(gardenDate, "dddd dd/MM/yyyy"),
					Address = address,
					Bins = ProcessingUtilities.GetMatchingBins(_binTypes, "Garden"),
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
	/// Creates the client-side request for the bin collection search by postcode.
	/// </summary>
	private static ClientSideRequest CreateSearchRequest(string postcode)
	{
		return new ClientSideRequest
		{
			RequestId = 1,
			Url = $"https://www.fareham.gov.uk/internetlookups/search_data.aspx?type=CSV&list=DomesticBinCollections2025on&Road or Postcode={postcode}",
			Method = "GET",
		};
	}
}
