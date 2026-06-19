namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Xml.Linq;

/// <summary>
/// Collector implementation for East Riding of Yorkshire Council.
/// </summary>
internal sealed class EastRidingOfYorkshireCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "East Riding of Yorkshire Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.eastriding.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "east-riding-of-yorkshire";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "GreenDate" ],
		},
		new()
		{
			Name = "Garden and Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "BrownDate" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "BlueDate" ],
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
				Url = $"https://wasterecyclingapi.eastriding.gov.uk/api/RecyclingData/CollectionsData?APIKey=ekBWR8tSiv6qwMo31REEeTZ5FAiMNB&Licensee=BinCollectionWebTeam&Postcode={postcode}",
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
			var content = clientSideResponse.Content.TrimStart();

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			if (content.StartsWith('<'))
			{
				var xml = XDocument.Parse(clientSideResponse.Content);
				var ns = xml.Root!.GetDefaultNamespace();
				var rawAddresses = xml.Descendants(ns + "collectionDateOutput");

				foreach (var rawAddress in rawAddresses)
				{
					var uprn = rawAddress.Element(ns + "UPRN")!.Value.Trim();
					var greenDate = rawAddress.Element(ns + "GreenDate")!.Value.Trim();
					var brownDate = rawAddress.Element(ns + "BrownDate")!.Value.Trim();
					var blueDate = rawAddress.Element(ns + "BlueDate")!.Value.Trim();

					// Uid format: "uprn;greenDate;brownDate;blueDate"
					var address = new Address
					{
						Property = rawAddress.Element(ns + "Address")!.Value.Trim(),
						Postcode = postcode,
						Uid = $"{uprn};{greenDate};{brownDate};{blueDate}",
					};

					addresses.Add(address);
				}
			}
			else
			{
				using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);
				var rawAddresses = jsonDocument.RootElement.GetProperty("dataReturned").EnumerateArray();

				foreach (var rawAddress in rawAddresses)
				{
					var uprn = rawAddress.GetProperty("UPRN").GetString()!;
					var greenDate = rawAddress.GetProperty("GreenDate").GetString()!;
					var brownDate = rawAddress.GetProperty("BrownDate").GetString()!;
					var blueDate = rawAddress.GetProperty("BlueDate").GetString()!;

					// Uid format: "uprn;greenDate;brownDate;blueDate"
					var address = new Address
					{
						Property = rawAddress.GetProperty("Address").GetString()!.Trim(),
						Postcode = postcode,
						Uid = $"{uprn};{greenDate};{brownDate};{blueDate}",
					};

					addresses.Add(address);
				}
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
		// Process bin days from address Uid data
		if (clientSideResponse == null)
		{
			// Uid format: "uprn;greenDate;brownDate;blueDate"
			var uidParts = address.Uid!.Split(';', 4);

			var collectionEntries = new[]
			{
				new
				{
					Service = "GreenDate",
					CollectionDate = uidParts[1],
				},
				new
				{
					Service = "BrownDate",
					CollectionDate = uidParts[2],
				},
				new
				{
					Service = "BlueDate",
					CollectionDate = uidParts[3],
				},
			};

			// Iterate through each collection entry, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var collectionEntry in collectionEntries)
			{
				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, collectionEntry.Service);

				var binDay = new BinDay
				{
					Date = DateUtilities.ParseDateExact(collectionEntry.CollectionDate, "yyyy-MM-dd'T'HH:mm:ss"),
					Address = address,
					Bins = matchedBins,
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
