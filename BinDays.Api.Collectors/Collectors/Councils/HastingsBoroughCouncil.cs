namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Hastings Borough Council.
/// </summary>
internal sealed partial class HastingsBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Hastings Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.hastings.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "hastings";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Rubbish collection service" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling collection service" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden waste" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "Food waste" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// The base URL for the Hastings collection days web service.
	/// </summary>
	private const string _serviceBaseUrl = "https://el.hastings.gov.uk/MyArea/CollectionDays.asmx";

	/// <summary>
	/// Regex for parsing the Unix timestamp from a /Date(…)/ formatted JSON date string.
	/// </summary>
	[GeneratedRegex(@"\((\d+)\)")]
	private static partial Regex UnixDateRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var requestBody = JsonSerializer.Serialize(new { PostCode = postcode, PropertyNumber = "" });

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_serviceBaseUrl}/GetAddresses",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.ApplicationJson },
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
			using var document = JsonDocument.Parse(clientSideResponse.Content);
			var addressesElement = document.RootElement.GetProperty("d");

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var addressElement in addressesElement.EnumerateArray())
			{
				var address = new Address
				{
					Property = addressElement.GetProperty("display").GetString()!.Trim(),
					Postcode = postcode,
					Uid = addressElement.GetProperty("name").GetString()!,
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
			var requestBody = JsonSerializer.Serialize(new { Uprn = address.Uid });

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_serviceBaseUrl}/LookupCollectionDaysByService",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.ApplicationJson },
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
			using var document = JsonDocument.Parse(clientSideResponse.Content);
			var collectionsElement = document.RootElement.GetProperty("d");

			// Iterate through each collection service, and create bin day entries
			var binDays = new List<BinDay>();
			foreach (var collectionElement in collectionsElement.EnumerateArray())
			{
				var service = collectionElement.GetProperty("Service").GetString()!.Trim();
				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);
				var dates = collectionElement.GetProperty("Dates");

				// Iterate through each date, and create a bin day for the service
				foreach (var dateElement in dates.EnumerateArray())
				{
					var timestamp = long.Parse(UnixDateRegex().Match(dateElement.GetString()!).Groups[1].ValueSpan);
					var date = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(timestamp).Date);

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
