namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Exceptions;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

/// <summary>
/// Collector implementation for Bolsover District Council.
/// </summary>
internal sealed class BolsoverDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Bolsover District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.bolsover.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "bolsover";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Black" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = new("Burgundy", "#B8253F"),
			Keys = [ "Burgundy" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Green" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "Brown" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// The base URL for the Bolsover District Council self service portal.
	/// </summary>
	private const string _baseUrl = "https://selfservice.bolsover.gov.uk";

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for starting the session
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/service/Check_your_Bin_Day",
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for address lookup
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var requestBody = $$"""
			{
				"formValues": {
					"Section 1": {
						"postcode_search": { "value": "{{postcode}}" }
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/apibroker/runLookup?id=6058a5f55dc2e",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.ApplicationJson },
					{ "cookie", cookies },
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
		else if (clientSideResponse.RequestId == 2)
		{
			var rows = ParseLookupRows(clientSideResponse.Content);

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var row in rows)
			{
				var address = new Address
				{
					Property = row["display"],
					Postcode = postcode,
					Uid = row["uprn"],
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
		// Prepare client-side request for starting the session
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/authapi/isauthenticated",
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for collection route lookup
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var sid = jsonDoc.RootElement.GetProperty("auth-session").GetString()!;

			var requestBody = $$"""
			{
				"formValues": {
					"Bin Collection": {
						"uprnLoggedIn": { "value": "{{address.Uid}}" }
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/apibroker/runLookup?id=6023d37e037c3",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.ApplicationJson },
					{ "cookie", cookies },
				},
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", cookies },
						{ "sid", sid },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for black bin week lookup
		else if (clientSideResponse.RequestId == 2)
		{
			var metadata = clientSideResponse.Options.Metadata;
			var rows = ParseLookupRows(clientSideResponse.Content);

			// Rural, newly built and commercial properties have no collection route
			if (rows.Count == 0)
			{
				throw new BinDaysNotFoundException(GovUkId, address.Postcode!, address.Uid!);
			}

			// Route is prefixed with the collection day (e.g. "ThS" for Thursday)
			var route = rows.Single()["Route"];
			var collectionDay = route[..2] switch
			{
				"Mo" => DayOfWeek.Monday,
				"Tu" => DayOfWeek.Tuesday,
				"We" => DayOfWeek.Wednesday,
				"Th" => DayOfWeek.Thursday,
				"Fr" => DayOfWeek.Friday,
				_ => throw new InvalidOperationException($"Unknown collection route: {route}"),
			};

			// Next collection is today if it falls on the collection day, as on the council website
			var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Europe/London"));
			var date = today.AddDays(((int)collectionDay - (int)today.DayOfWeek + 7) % 7);

			var clientSideRequest = CreateWeekLookupRequest(3, "6023d2785d11f", metadata["cookie"], route, date, new()
			{
				{ "cookie", metadata["cookie"] },
				{ "sid", metadata["sid"] },
				{ "route", route },
				{ "date", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
			});

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for burgundy and green bin week lookup
		else if (clientSideResponse.RequestId == 3)
		{
			var metadata = clientSideResponse.Options.Metadata;
			var date = DateUtilities.ParseDateExact(metadata["date"], "yyyy-MM-dd");
			var blackWeek = ParseLookupRows(clientSideResponse.Content).Count > 0;

			var clientSideRequest = CreateWeekLookupRequest(4, "6023d2c7f3795", metadata["cookie"], metadata["route"], date, new()
			{
				{ "cookie", metadata["cookie"] },
				{ "sid", metadata["sid"] },
				{ "date", metadata["date"] },
				{ "blackWeek", blackWeek.ToString() },
			});

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for the form definition, which holds the green bin winter suspension dates
		else if (clientSideResponse.RequestId == 4)
		{
			var metadata = clientSideResponse.Options.Metadata;
			var blackWeek = bool.Parse(metadata["blackWeek"]);
			var burgundyWeek = ParseLookupRows(clientSideResponse.Content).Count > 0;

			// The council has not yet published the schedule for this week (e.g. ISO week 53 or the next year)
			if (!blackWeek && !burgundyWeek)
			{
				throw new BinDaysNotFoundException(GovUkId, address.Postcode!, address.Uid!);
			}

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 5,
				Url = $"{_baseUrl}/api/get-document/json?uri=sandbox-publish://AF-Process-d058fcb0-ae09-4b98-a33a-4f74ccd078a8/AF-Stage-bbf14e7c-f21b-45de-b793-72fd5988f34d/definition.json&sid={metadata["sid"]}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "cookie", metadata["cookie"] },
				},
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "date", metadata["date"] },
						{ "blackWeek", metadata["blackWeek"] },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 5)
		{
			var metadata = clientSideResponse.Options.Metadata;
			var date = DateUtilities.ParseDateExact(metadata["date"], "yyyy-MM-dd");
			var blackWeek = bool.Parse(metadata["blackWeek"]);

			// Form definition is base64 encoded JSON, with the suspension dates as field default values
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var content = jsonDoc.RootElement.GetProperty("data").GetProperty("content").GetString()!;

			using var definitionDoc = JsonDocument.Parse(Convert.FromBase64String(content));
			var fields = definitionDoc.RootElement.GetProperty("sections").EnumerateArray()
				.SelectMany(section => section.GetProperty("fields").EnumerateArray())
				.Select(field => field.GetProperty("props"))
				.ToDictionary(props => props.GetProperty("dataName").GetString()!);

			var greenSuspensionStart = DateUtilities.ParseDateExact(fields["greenSusStart"].GetProperty("defaultValue").GetString()!, "yyyy-MM-dd");
			var greenSuspensionEnd = DateUtilities.ParseDateExact(fields["greenSusEnd"].GetProperty("defaultValue").GetString()!, "yyyy-MM-dd");

			// Iterate through the next four weeks shown on the council website, which alternate between rounds
			var binDays = new List<BinDay>();
			for (var week = 0; week < 4; week++)
			{
				var collectionDate = date.AddDays(week * 7);

				var service = ((week % 2 == 0) == blackWeek, collectionDate >= greenSuspensionStart && collectionDate < greenSuspensionEnd) switch
				{
					(true, _) => "Black & Brown Bins",
					(false, true) => "Burgundy & Brown Bins",
					(false, false) => "Burgundy, Green & Brown Bins",
				};

				var binDay = new BinDay
				{
					Date = collectionDate,
					Address = address,
					Bins = ProcessingUtilities.GetMatchingBins(_binTypes, service),
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

	/// <summary>
	/// Parses the rows of the XML data embedded in an API broker lookup response.
	/// </summary>
	private static List<Dictionary<string, string>> ParseLookupRows(string content)
	{
		using var jsonDoc = JsonDocument.Parse(content);
		var xmlData = jsonDoc.RootElement.GetProperty("data").GetString()!;

		var rows = XDocument.Parse(xmlData).Descendants("Row");

		return [.. rows.Select(row => row.Elements("result").ToDictionary(e => e.Attribute("column")!.Value, e => e.Value.Trim()))];
	}

	/// <summary>
	/// Creates the client-side request for checking whether a bin round is collected in the week of the given date.
	/// </summary>
	private static ClientSideRequest CreateWeekLookupRequest(int requestId, string lookupId, string cookies, string route, DateOnly date, Dictionary<string, string> metadata)
	{
		var dateTime = date.ToDateTime(TimeOnly.MinValue);

		var requestBody = $$"""
		{
			"formValues": {
				"Bin Collection": {
					"week": { "value": "{{ISOWeek.GetWeekOfYear(dateTime)}}" },
					"year": { "value": "{{ISOWeek.GetYear(dateTime)}}" },
					"Route": { "value": "{{route}}" }
				}
			}
		}
		""";

		var clientSideRequest = new ClientSideRequest
		{
			RequestId = requestId,
			Url = $"{_baseUrl}/apibroker/runLookup?id={lookupId}",
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", Constants.ApplicationJson },
				{ "cookie", cookies },
			},
			Body = requestBody,
			Options = new ClientSideOptions
			{
				Metadata = metadata,
			},
		};

		return clientSideRequest;
	}
}
