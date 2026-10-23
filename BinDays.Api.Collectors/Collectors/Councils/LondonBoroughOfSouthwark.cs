namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for London Borough of Southwark.
/// </summary>
internal sealed partial class LondonBoroughOfSouthwark : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "London Borough of Southwark";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.southwark.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "southwark";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	/// <remarks>
	/// The service names are the same for houses, flats with communal bins and flats above shops, so the bins are
	/// matched on the container image shown for each service (e.g. "AWCRubbishBin" or "CommunalRubbishBin").
	/// </remarks>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "AWCRubbishBin" ],
		},
		new()
		{
			Name = "Communal General Waste",
			Colour = BinColour.Green,
			Keys = [ "CommunalRubbishBin" ],
			Type = BinType.Container,
		},
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "RefuseBlackSack" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "AWCRecyclingBin" ],
		},
		new()
		{
			Name = "Communal Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "CommunalRecyclingBin" ],
			Type = BinType.Container,
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Clear,
			Keys = [ "RecyclingClearSack" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "FoodContainer" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Communal Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "CommunalFoodCollection" ],
			Type = BinType.Container,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "GardenWasteBIn" ],
		},
	];

	/// <summary>
	/// Regex for the container image and next collection date of each service on the schedules page.
	/// </summary>
	[GeneratedRegex(@"class=""binProduct(?:(?!class=""binProduct).)*?<img src=""/graphics/wasteServices/(?<container>[^"".]+)\.png""(?:(?!class=""binProduct).)*?Next collection: (?<date>[^<]+)</p>", RegexOptions.Singleline)]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://services.southwark.gov.uk/llpg/lookup?postcode={postcode}&classification=R",
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
			// The response is JSONP without a callback name, e.g. "([...]);"
			var json = clientSideResponse.Content.Trim().TrimStart('(').TrimEnd(';').TrimEnd(')');

			using var jsonDoc = JsonDocument.Parse(json);

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in jsonDoc.RootElement.EnumerateArray())
			{
				var addressLine1 = rawAddress.GetProperty("AddressLine1").GetString()!;
				var addressLine2 = rawAddress.GetProperty("AddressLine2").GetString()!;

				var address = new Address
				{
					Property = addressLine2 == string.Empty ? addressLine1 : $"{addressLine1}, {addressLine2}",
					Postcode = postcode,
					Uid = rawAddress.GetProperty("Uprn").GetString()!,
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
				Url = $"https://services.southwark.gov.uk/bins/lookup/{address.Uid}",
				Method = "GET",
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
			var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				// Parse the date (e.g. "Tue, 27 October 2026")
				var date = DateUtilities.ParseDateExact(rawBinDay.Groups["date"].Value.Trim(), "ddd, dd MMMM yyyy");

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, rawBinDay.Groups["container"].Value);

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = matchedBinTypes,
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
}
