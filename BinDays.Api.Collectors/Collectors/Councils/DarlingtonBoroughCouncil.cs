namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Darlington Borough Council.
/// </summary>
internal sealed partial class DarlingtonBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Darlington Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.darlington.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "darlington";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Refuse" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling: Bin" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling: Box" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Glass Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling:" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Food waste" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden Waste" ],
		},
	];

	/// <summary>
	/// Regex for each collection from the data, with its list of services and its date. The garden waste
	/// collection has no date when the address is not signed up, so each match is kept within a single collection.
	/// </summary>
	[GeneratedRegex(@"nextCollectionLabel(?<services>(?:(?!nextCollectionLabel)[\s\S])*?)<div class=""collectionDate[^>]*>\s*<p[^>]*>(?<date>\w+ \d+ \w+ \d{4})")]
	private static partial Regex BinDaysRegex();

	/// <summary>
	/// Regex for the services from a collection's list of services.
	/// </summary>
	[GeneratedRegex(@"collection-result-text"">(?<service>[^<]+)<")]
	private static partial Regex ServiceRegex();

	/// <summary>
	/// Regex for the recycling scheme from a collection's list of services, which is "Bin" for homes with
	/// a recycling bin and "Box" for homes with a recycling box.
	/// </summary>
	[GeneratedRegex(@"Scheme:</strong>\s*(?<scheme>\w+)")]
	private static partial Regex SchemeRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://www.darlington.gov.uk/postaladdress/GetAddressesByPostcode/{postcode}?local=true",
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
			var rawAddresses = jsonDoc.RootElement.GetProperty("results").EnumerateArray();

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in rawAddresses)
			{
				var address = new Address
				{
					Property = rawAddress.GetProperty("description").GetString()!.Trim(),
					Postcode = postcode,
					Uid = rawAddress.GetProperty("id").GetString()!,
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
				Url = $"https://www.darlington.gov.uk/bins-waste-and-recycling/collection-day-lookup/?uprn={address.Uid}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "x-requested-with", Constants.XmlHttpRequest },
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
			var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var date = DateUtilities.ParseDateExact(rawBinDay.Groups["date"].Value, "dddd d MMMM yyyy");
				var rawServices = ServiceRegex().Matches(rawBinDay.Groups["services"].Value)!;
				var scheme = SchemeRegex().Match(rawBinDay.Groups["services"].Value).Groups["scheme"].Value;

				// Iterate through each service collected on the date, and create a new bin day object
				foreach (Match rawService in rawServices)
				{
					// The recycling container depends on the scheme, so match on the service and scheme together
					// (e.g. "Recycling: Bin")
					var service = $"{rawService.Groups["service"].Value}: {scheme}";
					var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

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
