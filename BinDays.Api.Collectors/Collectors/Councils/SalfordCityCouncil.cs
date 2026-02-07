namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Salford City Council.
/// </summary>
internal sealed partial class SalfordCityCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Salford City Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.salford.gov.uk");

	/// <inheritdoc/>
	public override string GovUkId => "salford";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "Household Waste",
			Colour = BinColour.Black,
			Keys = [ "Black bin" ],
		},
		new()
		{
			Name = "Food and Garden Waste",
			Colour = BinColour.Pink,
			Keys = [ "Pink lidded bin" ],
		},
		new()
		{
			Name = "Paper and Cardboard",
			Colour = BinColour.Blue,
			Keys = [ "Blue bin" ],
		},
		new()
		{
			Name = "Glass, Cans and Plastics",
			Colour = BinColour.Brown,
			Keys = [ "Brown bin" ],
		},
	];

	/// <summary>
	/// Regex for ICS events.
	/// </summary>
	[GeneratedRegex(@"SUMMARY:(?<summary>.+?)\r?\n.*?DTSTART; ?VALUE ?= ?DATE:(?<date>\d{8})", RegexOptions.Singleline)]
	private static partial Regex BinEventRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://www.salford.gov.uk/umbraco/api/SalfordAPI/AddressSearch",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", "application/x-www-form-urlencoded; charset=UTF-8" },
				},
				Body = $"QueryStr={postcode}",
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
			using var addressesJson = JsonDocument.Parse(clientSideResponse.Content);
			var addressesElement = addressesJson.RootElement.GetProperty("addresses").EnumerateArray();

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var addressElement in addressesElement)
			{
				var address = new Address
				{
					Property = addressElement.GetProperty("address").GetString()!.Trim(),
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
				Url = $"https://www.salford.gov.uk/umbraco/api/salfordapi/GetBinCollectionsICS/?UPRN={address.Uid}",
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
			var rawCollections = BinEventRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin collection, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawCollection in rawCollections)
			{
				var summary = rawCollection.Groups["summary"].Value.Trim();
				var dateString = rawCollection.Groups["date"].Value;

				var date = DateOnly.ParseExact(
					dateString,
					"yyyyMMdd",
					CultureInfo.InvariantCulture,
					DateTimeStyles.None
				);

				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, summary);

				var binDay = new BinDay
				{
					Date = date,
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
