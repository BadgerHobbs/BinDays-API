namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Canterbury City Council.
/// </summary>
internal sealed partial class CanterburyCityCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Canterbury City Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.canterbury.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "canterbury";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "blackBinDay" ],
		},
		new()
		{
			Name = "Glass, Tins & Plastics Recycling",
			Colour = BinColour.Blue,
			Keys = [ "recyclingBinDay" ],
		},
		new()
		{
			Name = "Paper & Card Recycling",
			Colour = BinColour.Red,
			Keys = [ "recyclingBinDay" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Black,
			Keys = [ "foodBinDay" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "gardenBinDay" ],
		},
	];

	/// <summary>
	/// Regex for the addresses from the options elements.
	/// </summary>
	[GeneratedRegex(@"<option value=""(?<uid>\d+),\d+"">(?<address>[^<]+)</option>")]
	private static partial Regex AddressRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://www.canterbury.gov.uk/bins-and-waste/find-your-bin-collection-dates?postcode={postcode}",
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
		if (clientSideResponse == null)
		{
			var requestBody = $$"""
			{
				"uprn": "{{address.Uid}}"
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://n6ljrw455m.execute-api.eu-west-2.amazonaws.com/prod/get-bin-dates",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.ApplicationJson },
				},
				Body = requestBody,
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
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rawDates = jsonDoc.RootElement.GetProperty("dates");

			// Iterate through each collection type, and create a new bin day object for each of its dates
			var binDays = new List<BinDay>();
			foreach (var collectionType in new[] { "blackBinDay", "recyclingBinDay", "gardenBinDay", "foodBinDay", "communalFoodBinDay" })
			{
				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, collectionType);

				// Iterate through each bin day, and create a new bin day object
				foreach (var rawBinDay in rawDates.GetProperty(collectionType).EnumerateArray())
				{
					var binDay = new BinDay
					{
						Date = DateUtilities.ParseDateExact(rawBinDay.GetString()!, "yyyy-MM-dd'T'HH:mm:ss"),
						Address = address,
						Bins = matchedBinTypes,
					};

					binDays.Add(binDay);
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
