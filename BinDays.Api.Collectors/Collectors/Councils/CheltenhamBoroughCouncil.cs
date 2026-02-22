namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Cheltenham Borough Council.
/// </summary>
internal sealed partial class CheltenhamBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Cheltenham Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.cheltenham.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "cheltenham";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "Refuse" ],
		},
		new()
		{
			Name = "Mixed Dry Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Paper, Glass & Cardboard Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
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
	/// The base URL for StatMap Aurora requests.
	/// </summary>
	private const string _auroraBaseUrl = "https://maps.cheltenham.gov.uk/map/Aurora.svc";

	/// <summary>
	/// The script path used when requesting a new session.
	/// </summary>
	private const string _scriptPath = "%5CAurora%5CCBC%20Waste%20Streets.AuroraScript%24";

	/// <summary>
	/// Regex to strip HTML bold tags from location descriptions.
	/// </summary>
	[GeneratedRegex(@"</?b>", RegexOptions.IgnoreCase)]
	private static partial Regex HtmlBoldTagRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = CreateSessionRequest(1);

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request to find locations for the postcode
		else if (clientSideResponse.RequestId == 1)
		{
			var jsonDocument = ParseJsonp(clientSideResponse.Content);
			var sessionId = jsonDocument.RootElement.GetProperty("Session").GetProperty("SessionId").GetString()!;

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_auroraBaseUrl}/FindLocation?sessionId={sessionId}&address={postcode}&limit=100&callback=_jqjsp",
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
		else if (clientSideResponse.RequestId == 2)
		{
			var responseJson = ParseJsonp(clientSideResponse.Content);
			var locations = responseJson.RootElement.GetProperty("Locations").EnumerateArray();

			// Iterate through each location, and create a new address object
			var addresses = new List<Address>();
			foreach (var location in locations)
			{
				var description = HtmlBoldTagRegex().Replace(
					location.GetProperty("Description").GetString()!,
					string.Empty
				).Trim();

				var address = new Address
				{
					Property = description,
					Postcode = postcode,
					Uid = location.GetProperty("Id").GetString()!,
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
		// Prepare client-side request for creating a session
		if (clientSideResponse == null)
		{
			var clientSideRequest = CreateSessionRequest(1);

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for retrieving workflow tasks
		else if (clientSideResponse.RequestId == 1)
		{
			var responseJson = ParseJsonp(clientSideResponse.Content);
			var sessionId = responseJson.RootElement.GetProperty("Session").GetProperty("SessionId").GetString()!;

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_auroraBaseUrl}/GetWorkflow?sessionId={sessionId}&workflowId=wastestreet&callback=_jqjsp",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "sessionId", sessionId },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request to open the script map
		else if (clientSideResponse.RequestId == 2)
		{
			var workflowJson = ParseJsonp(clientSideResponse.Content);
			var sessionId = clientSideResponse.Options!.Metadata["sessionId"];

			var taskIds = workflowJson.RootElement.GetProperty("Tasks").EnumerateArray().ToDictionary(
				task => task.GetProperty("$type").GetString()!,
				task => task.GetProperty("Id").GetString()!
			);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"{_auroraBaseUrl}/OpenScriptMap?sessionId={sessionId}&callback=_jqjsp",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "sessionId", sessionId },
						{ "restoreTaskId", taskIds["StatMap.Aurora.RestoreStateTask, StatMapService"] },
						{ "saveTaskId", taskIds["StatMap.Aurora.SaveStateTask, StatMapService"] },
						{ "clearTaskId", taskIds["StatMap.Aurora.ClearResultSetTask, StatMapService"] },
						{ "visibilityTaskId", taskIds["StatMap.Aurora.ChangeLayersVisibilityTask, StatMapService"] },
						{ "drillTaskId", taskIds["StatMap.Aurora.DrillDownTask, StatMapService"] },
						{ "fetchTaskId", taskIds["StatMap.Aurora.FetchResultSetTask, StatMapService"] },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request to restore state
		else if (clientSideResponse.RequestId == 3)
		{
			var sessionId = clientSideResponse.Options!.Metadata["sessionId"];
			var restoreTaskId = clientSideResponse.Options.Metadata["restoreTaskId"];

			var job = """
				{
					"Task": {
						"$type": "StatMap.Aurora.RestoreStateTask, StatMapService"
					}
				}
				""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 4,
				Url = $"{_auroraBaseUrl}/ExecuteTaskJob?sessionId={sessionId}&taskId={restoreTaskId}&job={job}&callback=_jqjsp",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions
				{
					Metadata = new()
					{
						{ "sessionId", sessionId },
						{ "saveTaskId", clientSideResponse.Options.Metadata["saveTaskId"] },
						{ "clearTaskId", clientSideResponse.Options.Metadata["clearTaskId"] },
						{ "visibilityTaskId", clientSideResponse.Options.Metadata["visibilityTaskId"] },
						{ "drillTaskId", clientSideResponse.Options.Metadata["drillTaskId"] },
						{ "fetchTaskId", clientSideResponse.Options.Metadata["fetchTaskId"] },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request to save state
		else if (clientSideResponse.RequestId == 4)
		{
			var sessionId = clientSideResponse.Options!.Metadata["sessionId"];
			var saveTaskId = clientSideResponse.Options.Metadata["saveTaskId"];

			var job = """
				{
					"Task": {
						"$type": "StatMap.Aurora.SaveStateTask, StatMapService"
					}
				}
				""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 5,
				Url = $"{_auroraBaseUrl}/ExecuteTaskJob?sessionId={sessionId}&taskId={saveTaskId}&job={job}&callback=_jqjsp",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions
				{
					Metadata = new()
					{
						{ "sessionId", sessionId },
						{ "clearTaskId", clientSideResponse.Options.Metadata["clearTaskId"] },
						{ "visibilityTaskId", clientSideResponse.Options.Metadata["visibilityTaskId"] },
						{ "drillTaskId", clientSideResponse.Options.Metadata["drillTaskId"] },
						{ "fetchTaskId", clientSideResponse.Options.Metadata["fetchTaskId"] },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request to clear the result set
		else if (clientSideResponse.RequestId == 5)
		{
			var sessionId = clientSideResponse.Options!.Metadata["sessionId"];
			var clearTaskId = clientSideResponse.Options.Metadata["clearTaskId"];

			var job = """
				{
					"Task": {
						"$type": "StatMap.Aurora.ClearResultSetTask, StatMapService"
					}
				}
				""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 6,
				Url = $"{_auroraBaseUrl}/ExecuteTaskJob?sessionId={sessionId}&taskId={clearTaskId}&job={job}&callback=_jqjsp",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions
				{
					Metadata = new()
					{
						{ "sessionId", sessionId },
						{ "visibilityTaskId", clientSideResponse.Options.Metadata["visibilityTaskId"] },
						{ "drillTaskId", clientSideResponse.Options.Metadata["drillTaskId"] },
						{ "fetchTaskId", clientSideResponse.Options.Metadata["fetchTaskId"] },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request to ensure layers are visible
		else if (clientSideResponse.RequestId == 6)
		{
			var sessionId = clientSideResponse.Options!.Metadata["sessionId"];
			var visibilityTaskId = clientSideResponse.Options.Metadata["visibilityTaskId"];

			var job = """
				{
					"Task": {
						"$type": "StatMap.Aurora.ChangeLayersVisibilityTask, StatMapService"
					}
				}
				""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 7,
				Url = $"{_auroraBaseUrl}/ExecuteTaskJob?sessionId={sessionId}&taskId={visibilityTaskId}&job={job}&callback=_jqjsp",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions
				{
					Metadata = new()
					{
						{ "sessionId", sessionId },
						{ "drillTaskId", clientSideResponse.Options.Metadata["drillTaskId"] },
						{ "fetchTaskId", clientSideResponse.Options.Metadata["fetchTaskId"] },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request to resolve the selected location
		else if (clientSideResponse.RequestId == 7)
		{
			var sessionId = clientSideResponse.Options!.Metadata["sessionId"];

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 8,
				Url = $"{_auroraBaseUrl}/FindLocation?sessionId={sessionId}&locationId={address.Uid}&callback=_jqjsp",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions
				{
					Metadata = new()
					{
						{ "sessionId", sessionId },
						{ "drillTaskId", clientSideResponse.Options.Metadata["drillTaskId"] },
						{ "fetchTaskId", clientSideResponse.Options.Metadata["fetchTaskId"] },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request to drill into the selected location
		else if (clientSideResponse.RequestId == 8)
		{
			var responseJson = ParseJsonp(clientSideResponse.Content);
			var sessionId = clientSideResponse.Options!.Metadata["sessionId"];
			var drillTaskId = clientSideResponse.Options.Metadata["drillTaskId"];

			var location = responseJson.RootElement.GetProperty("Locations").EnumerateArray().First();
			var queryX = location.GetProperty("X").GetDouble();
			var queryY = location.GetProperty("Y").GetDouble();

			var job = $$"""
				{
					"QueryX": {{queryX.ToString(CultureInfo.InvariantCulture)}},
					"QueryY": {{queryY.ToString(CultureInfo.InvariantCulture)}},
					"Task": {
						"Type": "StatMap.Aurora.DrillDownTask, StatMapService"
					}
				}
				""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 9,
				Url = $"{_auroraBaseUrl}/ExecuteTaskJob?sessionId={sessionId}&taskId={drillTaskId}&job={job}&callback=_jqjsp",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions
				{
					Metadata = new()
					{
						{ "sessionId", sessionId },
						{ "fetchTaskId", clientSideResponse.Options.Metadata["fetchTaskId"] },
						{ "queryX", queryX.ToString(CultureInfo.InvariantCulture) },
						{ "queryY", queryY.ToString(CultureInfo.InvariantCulture) },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request to fetch the result set
		else if (clientSideResponse.RequestId == 9)
		{
			var sessionId = clientSideResponse.Options!.Metadata["sessionId"];
			var fetchTaskId = clientSideResponse.Options.Metadata["fetchTaskId"];
			var queryX = clientSideResponse.Options.Metadata["queryX"];
			var queryY = clientSideResponse.Options.Metadata["queryY"];

			var job = $$"""
				{
					"QueryX": {{queryX}},
					"QueryY": {{queryY}},
					"Task": {
						"Type": "StatMap.Aurora.FetchResultSetTask, StatMapService"
					},
					"ResultSetName": "inspection"
				}
				""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 10,
				Url = $"{_auroraBaseUrl}/ExecuteTaskJob?sessionId={sessionId}&taskId={fetchTaskId}&job={job}&callback=_jqjsp",
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
		// Prepare client-side request for the calendar feed and store service configuration
		else if (clientSideResponse.RequestId == 10)
		{
			var fetchJson = ParseJsonp(clientSideResponse.Content);
			var table = fetchJson.RootElement
				.GetProperty("TaskResult")
				.GetProperty("DistanceOrderedSet")
				.GetProperty("ResultSet")
				.GetProperty("Tables")
				.EnumerateArray()
				.First();

			var columnNames = table.GetProperty("ColumnDefinitions")
				.EnumerateArray()
				.Select(column => column.GetProperty("ColumnName").GetString()!)
				.ToList();

			var record = table.GetProperty("Records").EnumerateArray().First().EnumerateArray().ToList();

			var recordData = columnNames.Zip(record).ToDictionary(
				pair => pair.First,
				pair => pair.Second.ToString().Trim()
			);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 11,
				Url = "https://calendar.google.com/calendar/ical/v7oettki6t1pi2p7s0j6q6121k%40group.calendar.google.com/public/full.ics",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions
				{
					Metadata = new()
					{
						{ "refuseDay", recordData["New_Refuse_Day_internal"] },
						{ "refuseWeek", recordData["Refuse_Week_External"] },
						{ "recyclingDay", recordData["New_Recycling_Round"] },
						{ "recyclingWeek", recordData["Amended_Recycling_Round"] },
						{ "foodDay", recordData["New_Food_Day"] },
						{ "foodWeek", recordData["New_Food_Week_Internal"] },
						{ "gardenDay", recordData["Garden_Bin_Crew"] },
						{ "gardenWeek", recordData["Garden_Bin_Week"] },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days using the calendar feed and service configuration
		else if (clientSideResponse.RequestId == 11)
		{
			var week1Start = GetWeekStartDate(clientSideResponse.Content, "Week 1");
			var week2Start = GetWeekStartDate(clientSideResponse.Content, "Week 2");
			var endDate = DateOnly.FromDateTime(DateTime.Now.AddMonths(12));

			var refuseDates = BuildCollectionDates(
				clientSideResponse.Options!.Metadata["refuseDay"],
				clientSideResponse.Options.Metadata["refuseWeek"],
				week1Start,
				week2Start,
				endDate
			);

			var recyclingDates = BuildCollectionDates(
				clientSideResponse.Options.Metadata["recyclingDay"],
				clientSideResponse.Options.Metadata["recyclingWeek"],
				week1Start,
				week2Start,
				endDate
			);

			var foodDates = BuildCollectionDates(
				clientSideResponse.Options.Metadata["foodDay"],
				clientSideResponse.Options.Metadata["foodWeek"],
				week1Start,
				week2Start,
				endDate
			);

			var refuseBins = ProcessingUtilities.GetMatchingBins(_binTypes, "Refuse");
			var recyclingBins = ProcessingUtilities.GetMatchingBins(_binTypes, "Recycling");
			var foodBins = ProcessingUtilities.GetMatchingBins(_binTypes, "Food");
			var gardenBins = ProcessingUtilities.GetMatchingBins(_binTypes, "Garden");

			// Iterate through each service and its dates, and create a bin day entry
			var binDays = new List<BinDay>();
			foreach (var (dates, bins) in new[] {
				(refuseDates, refuseBins),
				(recyclingDates, recyclingBins),
				(foodDates, foodBins),
			})
			{
				foreach (var date in dates)
				{
					binDays.Add(new BinDay
					{
						Address = address,
						Date = date,
						Bins = bins,
					});
				}
			}

			var gardenWeek = clientSideResponse.Options.Metadata["gardenWeek"];
			if (!string.IsNullOrWhiteSpace(clientSideResponse.Options.Metadata["gardenDay"])
				&& !string.IsNullOrWhiteSpace(gardenWeek))
			{
				var gardenDates = BuildCollectionDates(
					clientSideResponse.Options.Metadata["gardenDay"],
					gardenWeek,
					week1Start,
					week2Start,
					endDate
				);

				// Iterate through each garden waste date, and create a bin day entry
				foreach (var date in gardenDates)
				{
					binDays.Add(new BinDay
					{
						Address = address,
						Date = date,
						Bins = gardenBins,
					});
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

	/// <summary>
	/// Creates the initial client-side request for a new session.
	/// </summary>
	private static ClientSideRequest CreateSessionRequest(int requestId)
	{
		return new ClientSideRequest
		{
			RequestId = requestId,
			Url = $"{_auroraBaseUrl}/RequestSession?userName=guest%20CBC&password=&script={_scriptPath}&callback=_jqjsp",
			Method = "GET",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
			},
		};
	}

	/// <summary>
	/// Parses a JSONP response into a JSON document.
	/// </summary>
	private static JsonDocument ParseJsonp(string content)
	{
		var startIndex = content.IndexOf('(');
		var endIndex = content.LastIndexOf(')');

		if (startIndex == -1 || endIndex == -1 || endIndex <= startIndex)
		{
			throw new InvalidOperationException("Invalid JSONP response.");
		}

		var jsonContent = content[(startIndex + 1)..endIndex];

		return JsonDocument.Parse(jsonContent);
	}

	/// <summary>
	/// Builds recurring collection dates for a service based on the week pattern.
	/// </summary>
	private static List<DateOnly> BuildCollectionDates(
		string dayOfWeekText,
		string weekPattern,
		DateOnly week1Start,
		DateOnly week2Start,
		DateOnly endDate
	)
	{
		var dayOfWeek = Enum.TryParse(dayOfWeekText, true, out DayOfWeek parsedDay)
			? parsedDay
			: (DayOfWeek)Array.IndexOf(
				CultureInfo.InvariantCulture.DateTimeFormat.AbbreviatedDayNames,
				CultureInfo.InvariantCulture.DateTimeFormat.AbbreviatedDayNames
					.FirstOrDefault(d => d.Equals(dayOfWeekText, StringComparison.OrdinalIgnoreCase))
					?? throw new InvalidOperationException("Invalid day of week provided.")
			);

		string normalisedPattern;
		if (weekPattern.Contains("weekly", StringComparison.OrdinalIgnoreCase))
		{
			normalisedPattern = "Weekly";
		}
		else if (weekPattern.Contains('1') && weekPattern.Contains('2'))
		{
			normalisedPattern = "Weekly";
		}
		else if (weekPattern.Contains('2'))
		{
			normalisedPattern = "2";
		}
		else
		{
			normalisedPattern = "1";
		}

		var week1Date = GetFirstWeekDate(week1Start, dayOfWeek);
		var week2Date = GetFirstWeekDate(week2Start, dayOfWeek);

		if (normalisedPattern.Equals("Weekly", StringComparison.OrdinalIgnoreCase))
		{
			var firstDate = week1Date < week2Date ? week1Date : week2Date;
			return BuildRecurringDates(firstDate, 7, endDate);
		}

		var targetWeek = normalisedPattern == "2" ? week2Date : week1Date;

		return BuildRecurringDates(targetWeek, 14, endDate);
	}

	/// <summary>
	/// Builds a list of dates at a specified interval.
	/// </summary>
	private static List<DateOnly> BuildRecurringDates(DateOnly startDate, int intervalDays, DateOnly endDate)
	{
		var dates = new List<DateOnly>();

		for (var current = startDate; current <= endDate; current = current.AddDays(intervalDays))
		{
			dates.Add(current);
		}

		return dates;
	}

	/// <summary>
	/// Gets the first date for a given day within a week.
	/// </summary>
	private static DateOnly GetFirstWeekDate(DateOnly weekStart, DayOfWeek dayOfWeek)
	{
		var offset = ((int)dayOfWeek - (int)weekStart.DayOfWeek + 7) % 7;

		return weekStart.AddDays(offset);
	}

	/// <summary>
	/// Extracts the week start date for a specific summary label from the ICS content.
	/// </summary>
	private static DateOnly GetWeekStartDate(string icsContent, string summaryLabel)
	{
		var events = icsContent.Split("BEGIN:VEVENT", StringSplitOptions.RemoveEmptyEntries);

		// Iterate through each calendar event to locate the matching week summary
		foreach (var calendarEvent in events)
		{
			var lines = calendarEvent
				.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

			var summaryLine = lines.FirstOrDefault(line => line.StartsWith("SUMMARY", StringComparison.OrdinalIgnoreCase));

			if (summaryLine == null || !summaryLine.Contains(summaryLabel, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var startLine = lines.FirstOrDefault(line => line.StartsWith("DTSTART", StringComparison.OrdinalIgnoreCase));

			if (startLine == null || !startLine.Contains(':'))
			{
				continue;
			}

			var dateText = startLine.Split(':', 2)[1].Trim();

			var startDate = DateOnly.ParseExact(
				dateText,
				"yyyyMMdd",
				CultureInfo.InvariantCulture,
				DateTimeStyles.None
			);

			return startDate;
		}

		throw new InvalidOperationException("Week start date not found in calendar feed.");
	}
}
