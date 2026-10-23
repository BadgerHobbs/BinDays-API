namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Collector implementation for St Albans City and District Council.
/// </summary>
internal sealed class StAlbansCityAndDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "St Albans City and District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.stalbans.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "st-albans";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Brown,
			Keys = [ "Refuse" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Black,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Paper & Card Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Garden" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Food" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// The base URL of the council's Cadcorp Notice Board web services.
	/// </summary>
	private const string _baseUrl = "https://gis.stalbans.gov.uk/NoticeBoard9";

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var requestBody = $$"""
			{
				"searchId": 4,
				"filter": "{{postcode}}",
				"startIndex": 0,
				"endIndex": 199
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/quicksearch.asmx/GetMoreResults",
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
			var rawAddresses = jsonDoc.RootElement.GetProperty("d").GetProperty("Data");

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();

			// Postcodes with no matches return null rather than an empty list
			if (rawAddresses.ValueKind == JsonValueKind.Array)
			{
				foreach (var rawAddress in rawAddresses.EnumerateArray())
				{
					// Each result is a list of named columns (e.g. ADDRESS, UPRN)
					var columns = rawAddress.GetProperty("Columns").EnumerateArray().ToDictionary(
						column => column.GetProperty(nameof(Name)).GetString()!,
						column => column.GetProperty("Value")
					);

					var address = new Address
					{
						Property = columns["ADDRESS"].GetString()!,
						Postcode = postcode,
						Uid = columns["UPRN"].GetInt64().ToString(),
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
		// Prepare client-side request for getting bin days
		if (clientSideResponse == null)
		{
			var requestBody = $$"""
			{
				"uprn": "{{address.Uid}}",
				"noticeBoard": "default"
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/VeoliaProxy.NoticeBoard.asmx/GetServicesByUprnAndNoticeBoard",
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

			// Iterate through each service's task, and create a new bin day object for its next collection
			var binDays = new List<BinDay>();
			foreach (var rawService in jsonDoc.RootElement.GetProperty("d").EnumerateArray())
			{
				foreach (var rawTask in rawService.GetProperty("ServiceHeaders").EnumerateArray())
				{
					// Parse the date (e.g. "2026-10-29T00:00:00+00:00")
					var date = DateUtilities.ParseDateExact(rawTask.GetProperty("Next").GetString()![..10], "yyyy-MM-dd");

					var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, rawTask.GetProperty("TaskType").GetString()!);

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
