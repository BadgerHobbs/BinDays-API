namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Collector implementation for Wealden District Council.
/// </summary>
internal sealed class WealdenDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Wealden District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.wealden.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "wealden";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Refuse", "Rubbish" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Food" ],
			Type = BinType.Caddy,
		},
	];

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "action", "wealden_get_properties_in_postcode" },
				{ "postcode", postcode },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://www.wealden.gov.uk/wp-admin/admin-ajax.php",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
				},
				Body = requestBody,
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
			var properties = jsonDoc.RootElement.GetProperty("properties").EnumerateArray();

			// Iterate through each property, and create a new address object
			var addresses = new List<Address>();
			foreach (var propertyElement in properties)
			{
				var address = new Address
				{
					Property = propertyElement.GetProperty("address").GetString()!.Trim(),
					Postcode = postcode,
					Uid = propertyElement.GetProperty("uprn").GetString()!,
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
			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "action", "wealden_get_collections_for_uprn" },
				{ "uprn", address.Uid! },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://www.wealden.gov.uk/wp-admin/admin-ajax.php",
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
		// Process bin days from response
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var collection = jsonDoc.RootElement.GetProperty("collection");

			var binDays = new List<BinDay>();

			var binCollectionProperties = new Dictionary<string, string>
			{
				{ "refuseCollectionDate", "Refuse" },
				{ "recyclingCollectionDate", "Recycling" },
				{ "gardenCollectionDate", "Garden" },
				{ "foodCollectionDate", "Food" },
			};

			// Iterate through each bin collection property, and create a new bin day object
			foreach (var property in binCollectionProperties)
			{
				if (!collection.TryGetProperty(property.Key, out var dateElement))
				{
					continue;
				}

				var dateString = dateElement.GetString();

				if (string.IsNullOrWhiteSpace(dateString))
				{
					continue;
				}

				var date = DateUtilities.ParseDateExact(dateString, "yyyy-MM-dd'T'HH:mm:ss");

				var bins = ProcessingUtilities.GetMatchingBins(_binTypes, property.Value);

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = bins,
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
