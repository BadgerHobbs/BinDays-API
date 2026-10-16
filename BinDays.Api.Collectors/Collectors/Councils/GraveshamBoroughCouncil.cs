namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Gravesham Borough Council.
/// </summary>
internal sealed partial class GraveshamBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Gravesham Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.gravesham.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "gravesham";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "REFUSE" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Green,
			Keys = [ "RECYCLING" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Orange,
			Keys = [ "FOOD" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "GARDEN" ],
		},
	];

	/// <summary>
	/// The URL of the bin day checker form, used to start a session.
	/// </summary>
	private const string _sessionUrl = "https://my.gravesham.gov.uk/en/AchieveForms/?form_uri=sandbox-publish://AF-Process-22218d5c-c6d6-492f-b627-c713771126be/AF-Stage-905e87c1-144b-4a72-8932-5518ddd3e618/definition.json";

	/// <summary>
	/// The URL of the AchieveForms lookup endpoint.
	/// </summary>
	private const string _lookupUrl = "https://my.gravesham.gov.uk/apibroker/runLookup";

	/// <summary>
	/// Regex for the authentication token from the token lookup response.
	/// </summary>
	[GeneratedRegex(@"<result column=""tokenString""[^>]*>(?<token>[^<]+)</result>")]
	private static partial Regex TokenRegex();

	/// <summary>
	/// Regex for the collection date and bins from each row of the upcoming collections response.
	/// </summary>
	[GeneratedRegex(@"<result column=""Date""[^>]*>(?<date>[^<]+)</result><result column=""Name""[^>]*>(?<bins>[^<]+)</result>")]
	private static partial Regex BinDayRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for starting the session
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _sessionUrl,
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
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);

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
				Url = $"{_lookupUrl}?id=58c855b298b88",
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
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rawAddresses = jsonDoc.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("select_data");

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in rawAddresses.EnumerateArray())
			{
				var address = new Address
				{
					Property = rawAddress.GetProperty("label").GetString()!,
					Postcode = postcode,
					Uid = rawAddress.GetProperty("value").GetString()!,
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
		// Prepare client-side request for starting the session
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _sessionUrl,
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting the authentication token
		else if (clientSideResponse.RequestId == 1)
		{
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);

			var requestBody = """
			{
				"formValues": {}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_lookupUrl}?id=5ee8854759297",
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
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting the upcoming collections
		else if (clientSideResponse.RequestId == 2)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var xmlData = jsonDoc.RootElement.GetProperty("data").GetString()!;
			var token = TokenRegex().Match(xmlData).Groups["token"].Value;

			var cookies = clientSideResponse.Options.Metadata["cookie"];

			// Same 60 day window the council's bin day checker uses
			var today = DateOnly.FromDateTime(DateTime.Today);
			var dateFrom = today.ToString("yyyy-MM-dd");
			var dateTo = today.AddDays(60).ToString("yyyy-MM-dd");

			var requestBody = $$"""
			{
				"formValues": {
					"Section 1": {
						"tokenString": { "value": "{{token}}" },
						"UPRNForAPI": { "value": "{{address.Uid}}" },
						"formatDateToday": { "value": "{{dateFrom}}" },
						"formatDateTo": { "value": "{{dateTo}}" }
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"{_lookupUrl}?id=5c8f869376376",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.ApplicationJson },
					{ "cookie", cookies },
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
		else if (clientSideResponse.RequestId == 3)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var xmlData = jsonDoc.RootElement.GetProperty("data").GetString()!;

			var rawBinDays = BinDayRegex().Matches(xmlData)!;

			// Iterate through each collection, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				// Each row lists every bin emptied that day, e.g. "Empty Bin FOOD BIN Empty Bin REFUSE 180L"
				var bins = rawBinDay.Groups["bins"].Value;

				var date = DateUtilities.ParseDateExact(
					rawBinDay.Groups["date"].Value,
					"yyyy-MM-ddTHH:mm:ss"
				);

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = ProcessingUtilities.GetMatchingBins(_binTypes, bins),
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
