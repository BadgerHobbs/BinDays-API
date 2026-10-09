namespace BinDays.Api.Collectors.Collectors.Vendors;

using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Base collector implementation for councils using the Recollect waste collection widget.
/// </summary>
internal abstract class RecollectCollectorBase : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public abstract string Name { get; }

	/// <inheritdoc/>
	public abstract Uri WebsiteUrl { get; }

	/// <summary>
	/// Gets the Recollect area name for the council.
	/// </summary>
	protected abstract string AreaName { get; }

	/// <summary>
	/// Gets the Recollect service ID for the council's waste service.
	/// </summary>
	protected abstract string ServiceId { get; }

	/// <summary>
	/// Gets the list of bin types for this collector.
	/// </summary>
	protected abstract IReadOnlyCollection<Bin> BinTypes { get; }

	/// <summary>
	/// The base URL of the Recollect API.
	/// </summary>
	private const string _apiBaseUrl = "https://api.eu.recollect.net/api";

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting the postcode's qualifier
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_apiBaseUrl}/areas/{AreaName}/services/{ServiceId}/address-suggest?q={postcode}",
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for getting addresses
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var qualifierId = jsonDoc.RootElement[0].GetProperty("qualifier_id").GetString()!;

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_apiBaseUrl}/areas/{AreaName}/services/{ServiceId}/pages/en-GB/place_calendar.json",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "x-recollect-place", $"qualifier.{qualifierId}:{ServiceId}" },
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
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rawSections = jsonDoc.RootElement.GetProperty("sections").EnumerateArray();

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawSection in rawSections)
			{
				foreach (var rawAddress in rawSection.GetProperty("rows").EnumerateArray())
				{
					// Skip rows which are not addresses (e.g. the help message)
					if (!rawAddress.TryGetProperty("place_id", out var placeId))
					{
						continue;
					}

					// Place ID format: "placeId:serviceId:areaName"
					var address = new Address
					{
						Property = rawAddress.GetProperty("label").GetString()!.Trim(),
						Postcode = postcode,
						Uid = placeId.GetString()!.Split(':')[0],
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
			var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Europe/London"));

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_apiBaseUrl}/places/{address.Uid}/services/{ServiceId}/events?after={today:yyyy-MM-dd}&before={today.AddMonths(3):yyyy-MM-dd}",
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
			var rawBinDays = jsonDoc.RootElement.GetProperty("events").EnumerateArray();

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var rawBinDay in rawBinDays)
			{
				var date = DateUtilities.ParseDateExact(rawBinDay.GetProperty("day").GetString()!, "yyyy-MM-dd");

				// Iterate through each collection on the date, and create a new bin day object
				foreach (var rawFlag in rawBinDay.GetProperty("flags").EnumerateArray())
				{
					var matchedBinTypes = ProcessingUtilities.GetMatchingBins(BinTypes, rawFlag.GetProperty("name").GetString()!);

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
