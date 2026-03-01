namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Collector implementation for Bassetlaw District Council.
/// </summary>
internal sealed class BassetlawDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Bassetlaw District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.bassetlaw.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "bassetlaw";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "Green", "Custom" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Blue" ],
		},
	];

	/// <summary>
	/// The order of layers to query for bin data.
	/// </summary>
	private readonly int[] _layerOrder = [0, 1, 22, 3, 4];

	/// <summary>
	/// Base dates for the recycling and general waste rotations by layer id.
	/// </summary>
	private readonly Dictionary<int, (DateOnly Blue, DateOnly Green)> _baseDates = new()
	{
		{ 0, (new DateOnly(2022, 1, 31), new DateOnly(2018, 1, 1)) },
		{ 1, (new DateOnly(2022, 1, 25), new DateOnly(2022, 2, 1)) },
		{ 3, (new DateOnly(2022, 2, 3), new DateOnly(2018, 1, 1)) },
		{ 4, (new DateOnly(2022, 2, 4), new DateOnly(2022, 1, 28)) },
		{ 22, (new DateOnly(2022, 2, 2), new DateOnly(2022, 1, 26)) },
	};

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://utility.arcgis.com/usrsvcs/servers/6fc1ccfdd03249299be8216a9f596935/rest/services/OSPlacesCascade_UPRN_basic/GeocodeServer/findAddressCandidates?f=json&SingleLine={postcode}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
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
			var responseJson = JsonSerializer.Deserialize<JsonObject>(clientSideResponse.Content)!;
			var rawAddresses = responseJson["candidates"]!.AsArray()!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in rawAddresses)
			{
				var attributes = rawAddress!["attributes"]!.AsObject();
				var location = rawAddress["location"]!.AsObject();

				var uprn = attributes["UPRN"]!.GetValue<string>().Trim();
				var property = attributes["ADDRESS"]!.GetValue<string>().Trim();

				var x = location["x"]!.GetValue<double>().ToString(CultureInfo.InvariantCulture);
				var y = location["y"]!.GetValue<double>().ToString(CultureInfo.InvariantCulture);

				var address = new Address
				{
					Property = property,
					Postcode = postcode,
					Uid = $"{uprn};{x};{y}",
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
		// Prepare client-side request for getting bin polygons
		if (clientSideResponse == null)
		{
			var coordinateParts = address.Uid!.Split(';');
			var x = coordinateParts[1];
			var y = coordinateParts[2];

			var clientSideRequest = CreateLayerRequest(1, 0, x, y);

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process layer responses
		else if (clientSideResponse.RequestId is >= 1 and <= 5)
		{
			var layerPosition = int.Parse(clientSideResponse.Options.Metadata["layerPosition"], CultureInfo.InvariantCulture);
			var x = clientSideResponse.Options.Metadata["x"];
			var y = clientSideResponse.Options.Metadata["y"];

			var responseJson = JsonSerializer.Deserialize<JsonObject>(clientSideResponse.Content)!;
			var features = responseJson["features"]!.AsArray()!;

			if (features.Count == 0)
			{
				var nextLayerPosition = layerPosition + 1;

				if (nextLayerPosition >= _layerOrder.Length)
				{
					throw new InvalidOperationException("No bin data found for address.");
				}

				var nextClientSideRequest = CreateLayerRequest(clientSideResponse.RequestId + 1, nextLayerPosition, x, y);

				var nextLayerResponse = new GetBinDaysResponse
				{
					NextClientSideRequest = nextClientSideRequest,
				};

				return nextLayerResponse;
			}

			var layerId = _layerOrder[layerPosition];
			var attributes = features[0]!["attributes"]!.AsObject();

			var collectionDay = GetCollectionDay(attributes);

			var baseDates = _baseDates[layerId];
			var blueDates = BuildUpcomingDates(baseDates.Blue, collectionDay);
			var greenDates = BuildUpcomingDates(baseDates.Green, collectionDay);

			var binDays = new List<BinDay>();

			// Iterate through each general waste date, and create a new bin day object
			foreach (var date in greenDates)
			{
				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = ProcessingUtilities.GetMatchingBins(_binTypes, "Green"),
				};

				binDays.Add(binDay);
			}

			// Iterate through each recycling date, and create a new bin day object
			foreach (var date in blueDates)
			{
				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = ProcessingUtilities.GetMatchingBins(_binTypes, "Blue"),
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

	/// <summary>
	/// Creates a client-side request for a specific feature layer.
	/// </summary>
	private ClientSideRequest CreateLayerRequest(int requestId, int layerPosition, string x, string y)
	{
		var layerId = _layerOrder[layerPosition];

		var clientSideRequest = new ClientSideRequest
		{
			RequestId = requestId,
			Url = $"https://services1.arcgis.com/P2LV4qXI9z8W2RdA/arcgis/rest/services/Bassetlaw_District_Bin_Collection_WFL1/FeatureServer/{layerId}/query?f=json&geometry={x},{y}&geometryType=esriGeometryPoint&inSR=27700&spatialRel=esriSpatialRelIntersects&outFields=*",
			Method = "GET",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
			},
			Options = new ClientSideOptions
			{
				Metadata =
				{
					{ "x", x },
					{ "y", y },
					{ "layerPosition", layerPosition.ToString(CultureInfo.InvariantCulture) },
				},
			},
		};

		return clientSideRequest;
	}

	/// <summary>
	/// Determines the collection day from the feature attributes.
	/// </summary>
	private static DayOfWeek GetCollectionDay(JsonObject attributes)
	{
		var dayFields = new (string Field, DayOfWeek Day)[]
		{
			("Sunday", DayOfWeek.Sunday),
			("Monday", DayOfWeek.Monday),
			("Tuesday", DayOfWeek.Tuesday),
			("Wednesday", DayOfWeek.Wednesday),
			("Thursday", DayOfWeek.Thursday),
			("Friday", DayOfWeek.Friday),
			("Saturday", DayOfWeek.Saturday),
		};

		foreach (var dayField in dayFields)
		{
			if (attributes.TryGetPropertyValue(dayField.Field, out var valueNode))
			{
				var value = valueNode!.GetValue<string>().Trim();

				if (value.Equals("Yes", StringComparison.OrdinalIgnoreCase))
				{
					return dayField.Day;
				}
			}
		}

		var dayName = attributes["Day_"]!.GetValue<string>().Trim();

		return Enum.Parse<DayOfWeek>(dayName);
	}

	/// <summary>
	/// Builds a list of upcoming collection dates using the fortnightly schedule.
	/// </summary>
	private static IReadOnlyCollection<DateOnly> BuildUpcomingDates(DateOnly baseDate, DayOfWeek collectionDay)
	{
		const int pickupWeekInterval = 2;
		const int occurrences = 6;

		var today = DateOnly.FromDateTime(DateTime.UtcNow);

		var initialDate = GetNextCollectionDate(
			today,
			baseDate,
			collectionDay,
			pickupWeekInterval
		);

		var dates = new List<DateOnly>
		{
			initialDate,
		};

		for (var index = 1; index < occurrences; index++)
		{
			dates.Add(dates[index - 1].AddDays(pickupWeekInterval * 7));
		}

		return dates;
	}

	/// <summary>
	/// Calculates the next collection date based on the base week and pickup interval.
	/// </summary>
	private static DateOnly GetNextCollectionDate(
		DateOnly today,
		DateOnly baseDate,
		DayOfWeek collectionDay,
		int pickupWeekInterval
	)
	{
		var todayWeekStart = today.AddDays(0 - (int)today.DayOfWeek);
		var baseWeekStart = baseDate.AddDays(0 - (int)baseDate.DayOfWeek);

		var weeksBetween = (todayWeekStart.ToDateTime(TimeOnly.MinValue) - baseWeekStart.ToDateTime(TimeOnly.MinValue)).Days / 7;
		var weekRemainder = ((weeksBetween % pickupWeekInterval) + pickupWeekInterval) % pickupWeekInterval;

		var daysUntilCollection = ((int)collectionDay - (int)today.DayOfWeek + 7) % 7;

		if (weekRemainder > 0)
		{
			daysUntilCollection += 7 * (pickupWeekInterval - weekRemainder);
		}
		else if (daysUntilCollection == 0)
		{
			daysUntilCollection += pickupWeekInterval * 7;
		}

		return today.AddDays(daysUntilCollection);
	}
}
