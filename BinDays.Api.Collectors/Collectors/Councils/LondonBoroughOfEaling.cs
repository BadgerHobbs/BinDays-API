namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for London Borough of Ealing.
/// </summary>
internal sealed partial class LondonBoroughOfEaling : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "London Borough of Ealing";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.ealing.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "ealing";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "BLACK RUBBISH WHEELIE BIN" ],
		},
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "BLACK RUBBISH BAGS" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "COMMUNAL RUBBISH BIN" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "BLUE RECYCLING WHEELIE BIN" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Clear,
			Keys = [ "CLEAR RECYCLING BAGS" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Orange,
			Keys = [ "COMMUNAL RECYCLING BIN" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "FOOD BOX" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "GARDEN WASTE BIN" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "GARDEN WASTE BAGS" ],
			Type = BinType.Bag,
		},
	];

	/// <summary>
	/// The non-household services returned by the council, which are not collected for residents.
	/// </summary>
	private static readonly HashSet<string> _ignoredServices =
	[
		"",
		"COMMERCIAL RUBBISH BIN",
		"SCHOOLS FOOD WASTE",
	];

	/// <summary>
	/// Regex for matching whitespace.
	/// </summary>
	[GeneratedRegex(@"\s+")]
	private static partial Regex WhitespaceRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			Dictionary<string, string> formData = new()
			{
				{ "Postcode", postcode },
			};

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://www.ealing.gov.uk/site/custom_scripts/WasteCollectionWS/home/GetAddress",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(formData),
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
			var rawAddresses = jsonDoc.RootElement.GetProperty("param2");

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in rawAddresses.EnumerateArray())
			{
				var address = new Address
				{
					Property = WhitespaceRegex().Replace(rawAddress.GetProperty("Text").GetString()!, " ").Trim(),
					Postcode = postcode,
					Uid = rawAddress.GetProperty("Value").GetString()!,
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
			Dictionary<string, string> formData = new()
			{
				{ "UPRN", address.Uid! },
			};

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://www.ealing.gov.uk/site/custom_scripts/WasteCollectionWS/home/FindCollection",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(formData),
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
			var rawCollections = jsonDoc.RootElement.GetProperty("param2");

			// Iterate through each collection, and create a new bin day object for each date
			var binDays = new List<BinDay>();
			foreach (var rawCollection in rawCollections.EnumerateArray())
			{
				var service = rawCollection.GetProperty("Service").GetString()!;

				// Skip non-household services (e.g. trade waste, schools, grounds maintenance)
				if (_ignoredServices.Contains(service))
				{
					continue;
				}

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				foreach (var rawDate in rawCollection.GetProperty("collectionDate").EnumerateArray())
				{
					var date = DateUtilities.ParseDateExact(rawDate.GetString()!, "dd/MM/yyyy");

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
