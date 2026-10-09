namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Collector implementation for London Borough of Camden.
/// </summary>
internal sealed class LondonBoroughOfCamden : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "London Borough of Camden";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.camden.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "camden";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Rubbish collection" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling collection" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "Food collection" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden waste collection" ],
		},
	];

	/// <summary>
	/// The base URL of the council's collection services API.
	/// </summary>
	private const string _apiBaseUrl = "https://recyclingandrubbishcollections.camden.gov.uk/api";

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var requestBody = $$"""
			{
				"councilId": "27",
				"searchQuery": "{{postcode}}"
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_apiBaseUrl}/getPropertySearch",
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
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rawAddresses = jsonDoc.RootElement.GetProperty("data").EnumerateArray();

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in rawAddresses)
			{
				var address = new Address
				{
					Property = rawAddress.GetProperty("name").GetString()!.Trim(),
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
			var requestBody = $$"""
			{
				"councilId": "27",
				"pointId": "{{address.Uid}}",
				"pointType": "PointAddress"
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_apiBaseUrl}/getCollectionDays",
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
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rawServices = jsonDoc.RootElement.GetProperty("activeServices").EnumerateArray();

			// Iterate through each service, and create a new bin day object for each of its collections
			var binDays = new List<BinDay>();
			foreach (var rawService in rawServices)
			{
				var service = rawService.GetProperty("serviceName").GetString()!;
				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				// Iterate through the last and next collections, and create a new bin day object
				foreach (var rawSchedule in rawService.GetProperty("serviceSchedules").EnumerateArray())
				{
					// Take the date part of the timestamp (e.g. "2026-10-14T06:00:00+01:00")
					var collectionDate = rawSchedule.GetProperty("currentScheduledDate").GetString()![..10];

					var binDay = new BinDay
					{
						Date = DateUtilities.ParseDateExact(collectionDate, "yyyy-MM-dd"),
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
