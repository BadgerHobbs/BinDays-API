namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Collector implementation for Bedford Borough Council.
/// </summary>
internal sealed class BedfordBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Bedford Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.bedford.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "bedford";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Black bin" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Orange,
			Keys = [ "Orange bin" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Green bin" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "Caddy bin" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Communal Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "Food bin" ],
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
				Url = $"https://bbcpro-jeapi.azurewebsites.net/api/addresssearch/bypostcode?Postcode={postcode}&Language=ENG",
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
			foreach (var addressElement in jsonDoc.RootElement.GetProperty("data").EnumerateArray())
			{
				string[] addressParts =
				[
					addressElement.GetProperty("addressLine1").GetString()!,
					addressElement.GetProperty("addressLine2").GetString()!,
					addressElement.GetProperty("addressLine3").GetString()!,
					addressElement.GetProperty("town").GetString()!,
				];

				var address = new Address
				{
					Property = string.Join(", ", addressParts.Where(p => !string.IsNullOrWhiteSpace(p))),
					Postcode = postcode,
					Uid = addressElement.GetProperty("uprn").GetString()!,
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
				Url = $"https://bbaz-as-prod-bartecapi.azurewebsites.net/api/bincollections/residential/getbyuprn/{address.Uid}",
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
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

			// Iterate through each collection job, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var collectionGroup in jsonDoc.RootElement.GetProperty("BinCollections").EnumerateArray())
			{
				foreach (var jobElement in collectionGroup.EnumerateArray())
				{
					var binType = jobElement.GetProperty("BinType").GetString()!;
					var scheduledStart = jobElement.GetProperty("JobScheduledStart").GetString()!;

					var date = DateUtilities.ParseDateExact(scheduledStart, "yyyy-MM-ddTHH:mm:ss");

					var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, binType);

					var binDay = new BinDay
					{
						Date = date,
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
