namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

/// <summary>
/// Collector implementation for Barking and Dagenham Council.
/// </summary>
internal sealed class BarkingAndDagenhamCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Barking and Dagenham Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.lbbd.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "barking-and-dagenham";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "Non-Recyclable Waste",
			Colour = BinColour.Grey,
			Keys = [ "Grey-Household" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Brown,
			Keys = [ "Brown-Recycling" ],
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
				Url = $"https://www.lbbd.gov.uk/rest/bins/{postcode}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
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
			var addresses = new List<Address>();

			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var results = jsonDoc.RootElement.GetProperty("results");

			// Iterate through each address, and create a new address object
			foreach (var addressElement in results.EnumerateArray())
			{
				var address = new Address
				{
					Property = addressElement.GetProperty("address").GetString()!.Trim(),
					Postcode = postcode,
					Uid = addressElement.GetProperty("id").GetString()!,
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
				Url = $"https://www.lbbd.gov.uk/rest/bin/{address.Uid}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
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
			var binEntries = jsonDoc.RootElement.GetProperty("results");

			var binDays = new List<BinDay>();

			// Iterate through each bin entry, and add bin days for each collection date
			foreach (var binEntry in binEntries.EnumerateArray())
			{
				var binType = binEntry.GetProperty("bin_type").GetString()!;
				var collections = new List<string>();

				var nextCollection = binEntry.GetProperty("nextcollection").GetString()!;
				if (!string.IsNullOrWhiteSpace(nextCollection))
				{
					collections.Add(nextCollection);
				}

				foreach (var futureCollection in binEntry.GetProperty("futurecollections").EnumerateArray())
				{
					var futureDate = futureCollection.GetString();
					if (!string.IsNullOrWhiteSpace(futureDate))
					{
						collections.Add(futureDate);
					}
				}

				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, binType);

				foreach (var collectionDate in collections)
				{
					var date = DateOnly.ParseExact(
						collectionDate,
						"dddd dd MMMM yyyy",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);

					var binDay = new BinDay
					{
						Date = date,
						Address = address,
						Bins = matchedBins,
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
