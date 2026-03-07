namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Collector implementation for Bath and North East Somerset Council.
/// </summary>
internal sealed class BathAndNorthEastSomersetCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Bath and North East Somerset Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://app.bathnes.gov.uk/webforms/waste/collectionday/");

	/// <inheritdoc/>
	public override string GovUkId => "bath-and-north-east-somerset";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes = [
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Residual" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Card & Brown Paper",
			Colour = BinColour.Blue,
			Keys = [ "Recycling" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Metal, Glass, Paper & Plastic",
			Colour = BinColour.Green,
			Keys = [ "Recycling" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Garden" ],
		},
	];

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var requestUrl = $"https://app.bathnes.gov.uk/webapi/api/AddressesAPI/v2/search/{postcode}/150/true";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = requestUrl,
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
			var addresses = new List<Address>();

			// Parse response content as JSON array
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

			// Iterate through each address json, and create a new address object
			foreach (var addressElement in jsonDoc.RootElement.EnumerateArray())
			{
				var property = addressElement.GetProperty("full_Address").ToString();
				var uprn = addressElement.GetProperty("uprn").ToString().Split('.').First();

				var address = new Address
				{
					Property = property?.Trim(),
					Postcode = postcode,
					Uid = uprn,
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
			var requestUrl = $"https://app.bathnes.gov.uk/webapi/api/BinsAPI/v2/BartecFeaturesandSchedules/CollectionSummary/{address.Uid}";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = requestUrl,
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
			// Parse response content as JSON array
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rawBinDays = jsonDoc.RootElement;

			// Iterate through each service collection, and create bin day entries
			var binDays = new List<BinDay>();
			foreach (var rawBinDay in rawBinDays.EnumerateArray())
			{
				var collectionDate = rawBinDay.GetProperty("nextCollectionDate").GetString()!;

				if (string.IsNullOrWhiteSpace(collectionDate))
				{
					continue;
				}

				var date = DateUtilities.ParseDateExact(collectionDate, "yyyy-MM-ddTHH:mm:ss");

				var featureType = rawBinDay.GetProperty("featureType").GetString()!;
				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, featureType);

				if (matchedBinTypes.Count == 0)
				{
					continue;
				}

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
