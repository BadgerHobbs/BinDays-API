namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Collector implementation for Hull City Council.
/// </summary>
internal sealed class HullCityCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Hull City Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.hull.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "kingston-upon-hull";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Black Bin" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Blue Bin" ],
		},
		new()
		{
			Name = "Food & Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Brown Bin" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "GreenCaddy" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// The URL of the council's bin collection lookup endpoint.
	/// </summary>
	private const string _apiUrl = "https://www.hull.gov.uk/ajax/bin-collection";

	/// <summary>
	/// The council's bin collection day checker page, which the lookup requires as the referer.
	/// </summary>
	private const string _referer = "https://www.hull.gov.uk/bins-and-recycling/bin-collections/bin-collection-day-checker";

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_apiUrl}?postcode={postcode}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "referer", _referer },
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
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var results = jsonDoc.RootElement[0];

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var result in results.EnumerateArray())
			{
				var address = new Address
				{
					Property = result.GetProperty("prop_desc").GetString()!.Trim(),
					Postcode = postcode,
					Uid = result.GetProperty("uprn").GetString()!,
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
				Url = $"{_apiUrl}?bindate={address.Uid}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "referer", _referer },
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
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var collections = jsonDoc.RootElement[0];

			// Iterate through each collection, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var collection in collections.EnumerateArray())
			{
				var service = collection.GetProperty("collection_type").GetString()!.Trim();
				var date = DateUtilities.ParseDateExact(collection.GetProperty("next_collection_date").GetString()!, "yyyy-MM-dd");

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = ProcessingUtilities.GetMatchingBins(_binTypes, service),
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
