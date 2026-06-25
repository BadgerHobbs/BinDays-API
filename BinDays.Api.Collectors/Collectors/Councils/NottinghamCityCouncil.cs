namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Collector implementation for Nottingham City Council.
/// </summary>
internal sealed class NottinghamCityCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Nottingham City Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.nottinghamcity.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "nottingham";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "Residual" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Grey,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden" ],
		},
	];

	/// <summary>
	/// The base API URL for ReCollect.
	/// </summary>
	private const string _apiBaseUrl = "https://api.eu.recollect.net";

	/// <summary>
	/// The ReCollect area name for Nottingham City Council.
	/// </summary>
	private const string _areaName = "NottinghamCityCoun";

	/// <summary>
	/// The ReCollect service identifier for waste collections.
	/// </summary>
	private const string _serviceId = "50003";

	/// <summary>
	/// The locale used by the ReCollect API.
	/// </summary>
	private const string _locale = "en-GB";

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting postcode matches
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_apiBaseUrl}/api/areas/{_areaName}/services/{_serviceId}/address-suggest?q={Uri.EscapeDataString(postcode)}&locale={_locale}",
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for getting addresses in the postcode qualifier
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);

			var qualifierId = jsonDocument.RootElement[0].GetProperty("qualifier_id").GetString()!;

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_apiBaseUrl}/api/areas/{_areaName}/services/{_serviceId}/pages/{_locale}/place_calendar.json",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "x-recollect-place", $"qualifier.{qualifierId}:{_serviceId}" },
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 2)
		{
			using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);
			var addressRows = jsonDocument.RootElement.GetProperty("sections")[0].GetProperty("rows");

			// Iterate through each address row, and create a new address object
			var addresses = new List<Address>();
			foreach (var addressRow in addressRows.EnumerateArray())
			{
				var placeId = addressRow.GetProperty("place_id").GetString()!;
				var placeIdParts = placeId.Split(':', 2);

				var address = new Address
				{
					Property = addressRow.GetProperty("label").GetString()!.Trim(),
					Postcode = postcode,
					Uid = placeIdParts[0],
				};

				addresses.Add(address);
			}

			var getAddressesResponse = new GetAddressesResponse
			{
				Addresses = [.. addresses],
			};

			return getAddressesResponse;
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting bin day events
		if (clientSideResponse == null)
		{
			var after = DateTime.UtcNow.ToString("yyyy-MM-dd");
			var before = DateTime.UtcNow.AddMonths(3).ToString("yyyy-MM-dd");

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_apiBaseUrl}/api/places/{address.Uid!}/services/{_serviceId}/events?after={after}&before={before}&locale={_locale}",
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
			using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);
			var rawEvents = jsonDocument.RootElement.GetProperty("events");

			// Iterate through each event, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var rawEvent in rawEvents.EnumerateArray())
			{
				var matchingBins = new List<Bin>();

				// Iterate through each event flag, and map it to bins
				foreach (var rawFlag in rawEvent.GetProperty("flags").EnumerateArray())
				{
					if (rawFlag.GetProperty("event_type").GetString()! != "pickup")
					{
						continue;
					}

					var flagName = rawFlag.GetProperty("name").GetString()!;
					matchingBins.AddRange(ProcessingUtilities.GetMatchingBins(_binTypes, flagName));
				}

				if (matchingBins.Count == 0)
				{
					continue;
				}

				var binDay = new BinDay
				{
					Date = DateUtilities.ParseDateExact(rawEvent.GetProperty("day").GetString()!, "yyyy-MM-dd"),
					Address = address,
					Bins = matchingBins,
				};

				binDays.Add(binDay);
			}

			var getBinDaysResponse = new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};

			return getBinDaysResponse;
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}
}
