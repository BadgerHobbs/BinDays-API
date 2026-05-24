namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Collector implementation for Sheffield City Council.
/// </summary>
internal sealed class SheffieldCityCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Sheffield City Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://sheffield.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "sheffield";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "Non-Recyclable Waste",
			Colour = BinColour.Black,
			Keys = [ "Black Bin" ],
		},
		new()
		{
			Name = "Paper and Card Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Blue Bin" ],
		},
		new()
		{
			Name = "Glass, Tins and Plastic Bottles Recycling",
			Colour = BinColour.Brown,
			Keys = [ "Brown Bin" ],
		},
	];

	/// <summary>
	/// The base URL for the Sheffield waste services portal.
	/// </summary>
	private const string _baseUrl = "https://wasteservices.sheffield.gov.uk/api";

	/// <summary>
	/// The council identifier required by the Sheffield API.
	/// </summary>
	private const string _councilId = "1";

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var requestBody = $$"""
			{
				"councilId": "{{_councilId}}",
				"searchQuery": "{{postcode}}"
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Method = "POST",
				Url = $"{_baseUrl}/getPropertySearch",
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
			using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);
			var rawAddresses = jsonDocument.RootElement.GetProperty("data").EnumerateArray();

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in rawAddresses)
			{
				var id = rawAddress.GetProperty("id").GetString()!;
				var property = rawAddress.GetProperty("name").GetString()!.Trim();
				// Some addresses (e.g. converted properties) use a non-PointAddress type in getCollectionDays
				var type = rawAddress.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;

				var address = new Address
				{
					Property = property,
					Postcode = postcode,
					// Uid format: "id" or "id;pointType" for non-PointAddress records
					Uid = !string.IsNullOrEmpty(type) && type != "PointAddress" ? $"{id};{type}" : id,
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
			// Uid format: "id" or "id;pointType" for non-PointAddress records
			var uidParts = address.Uid!.Split(';', 2);
			var pointId = uidParts[0];
			var pointType = uidParts.Length == 2 ? uidParts[1] : "PointAddress";

			var requestBody = $$"""
			{
				"pointId": "{{pointId}}",
				"pointType": "{{pointType}}",
				"councilId": "{{_councilId}}"
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Method = "POST",
				Url = $"{_baseUrl}/getCollectionDays",
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
			using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);
			var rawServices = jsonDocument.RootElement.GetProperty("activeServices").EnumerateArray();

			// Iterate through each service, and create new bin day objects
			var binDays = new List<BinDay>();
			foreach (var rawService in rawServices)
			{
				var service = rawService.GetProperty("serviceName").GetString()!.Trim();
				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);
				var rawSchedules = rawService.GetProperty("serviceSchedules").EnumerateArray();

				// Iterate through each service schedule, and create a new bin day object
				foreach (var rawSchedule in rawSchedules)
				{
					var currentScheduledDate = rawSchedule.GetProperty("currentScheduledDate").GetString()!;
					var date = DateUtilities.ParseDateExact(currentScheduledDate[..10], "yyyy-MM-dd");

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
