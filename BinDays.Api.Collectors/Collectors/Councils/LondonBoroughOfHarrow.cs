namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for London Borough of Harrow.
/// </summary>
internal sealed partial class LondonBoroughOfHarrow : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "London Borough of Harrow";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.harrow.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "harrow";

	/// <summary>
	/// The list of bin types for household containers.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Grey,
			Keys = [ "RESIDUAL" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "RECYCLABLES" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Black,
			Keys = [ "FOOD" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "GARDEN" ],
		},
	];

	/// <summary>
	/// The list of bin types for communal containers, used by flats.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _communalBinTypes =
	[
		new()
		{
			Name = "Communal General Waste",
			Colour = BinColour.Blue,
			Keys = [ "RESIDUAL" ],
			Type = BinType.Container,
		},
		new()
		{
			Name = "Communal Mixed Recycling",
			Colour = BinColour.Grey,
			Keys = [ "RECYCLABLES" ],
			Type = BinType.Container,
		},
		new()
		{
			Name = "Communal Food Waste",
			Colour = BinColour.Red,
			Keys = [ "FOOD" ],
		},
	];

	/// <summary>
	/// The base URL of the council's bins API.
	/// </summary>
	private const string _apiBaseUrl = "https://www.harrow.gov.uk/ajax";

	/// <summary>
	/// Regex for the container size from a container name, e.g. "RESIDUAL-1100L".
	/// </summary>
	[GeneratedRegex(@"(?<size>\d+)L$")]
	private static partial Regex ContainerSizeRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_apiBaseUrl}/address?q={postcode}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "x-requested-with", Constants.XmlHttpRequest },
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
			var rawAddresses = jsonDoc.RootElement.GetProperty("results").EnumerateArray();

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in rawAddresses)
			{
				var address = new Address
				{
					Property = rawAddress.GetProperty("Display_Address").GetString()!,
					Postcode = postcode,
					Uid = rawAddress.GetProperty("UPRN").GetString()!,
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
				Url = $"{_apiBaseUrl}/bins/summary/{address.Uid}",
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
			var results = jsonDoc.RootElement.GetProperty("results");
			var containers = results.GetProperty("bins");
			var rawBinDays = results.GetProperty("collections").GetProperty("next").EnumerateArray();

			// Iterate through each next collection, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var rawBinDay in rawBinDays)
			{
				var binType = rawBinDay.GetProperty("binType").GetString()!;

				// Flats have large communal bins (660L and up) and a 140L food bin instead of the 23L caddy
				var containerName = containers.GetProperty(binType)[0].GetProperty("featureName").GetString()!;
				var containerSize = int.Parse(ContainerSizeRegex().Match(containerName).Groups["size"].Value);
				var isCommunal = binType == "FOOD" ? containerSize > 23 : containerSize >= 660;

				// Take the date part of the timestamp (e.g. "2026-10-20T00:00:00")
				var date = DateUtilities.ParseDateExact(
					rawBinDay.GetProperty("eventTime").GetString()![..10],
					"yyyy-MM-dd"
				);

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = ProcessingUtilities.GetMatchingBins(isCommunal ? _communalBinTypes : _binTypes, binType),
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
