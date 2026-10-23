namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

/// <summary>
/// Collector implementation for Stevenage Borough Council.
/// </summary>
internal sealed class StevenageBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Stevenage Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.stevenage.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "stevenage";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "General waste" ],
		},
		new()
		{
			Name = "Plastics & Metals",
			Colour = BinColour.Black,
			Keys = [ "Recycling" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Paper & Card",
			Colour = BinColour.Blue,
			Keys = [ "Recycling" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Glass",
			Colour = BinColour.Red,
			Keys = [ "Recycling" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Food waste" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden" ],
		},
	];

	/// <summary>
	/// The URL of the council's bin collection calendar form, used to start the session.
	/// </summary>
	private const string _sessionUrl = "https://stevenage-self.achieveservice.com/service/my_bin_collection_schedule";

	/// <summary>
	/// The URL of the council's Achieve Forms lookups.
	/// </summary>
	private const string _lookupUrl = "https://stevenage-self.achieveservice.com/apibroker/runLookup";

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
				Url = $"{_lookupUrl}?id=5bacaad975c70",
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
		// Prepare client-side request for getting the Bartec token required by the collection lookup
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_lookupUrl}?id=5e55337a540d4",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.ApplicationJson },
					{ "cookie", cookies },
				},
				Body = """{ "formValues": {} }""",
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
		// Prepare client-side request for getting the collection dates
		else if (clientSideResponse.RequestId == 2)
		{
			var cookies = clientSideResponse.Options.Metadata["cookie"];

			using var tokenDoc = JsonDocument.Parse(clientSideResponse.Content);
			var token = tokenDoc.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("rows_data")
				.GetProperty("0")
				.GetProperty("token")
				.GetString()!;

			// The council website lets residents search up to three months ahead, so ask for two months
			var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Europe/London"));
			var minDate = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
			var maxDate = today.AddMonths(2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

			var requestBody = $$"""
			{
				"formValues": {
					"Section 1": {
						"LLPGUPRN": { "value": "{{address.Uid}}" },
						"token": { "value": "{{token}}" },
						"MinimumDateLookAhead": { "value": "{{minDate}}" },
						"MaximumDateLookAhead": { "value": "{{maxDate}}" }
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"{_lookupUrl}?id=64ba8cee353e6",
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
			var rawBinDays = jsonDoc.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("rows_data");

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();

			// Properties without collections return an empty array rather than an object of rows
			if (rawBinDays.ValueKind == JsonValueKind.Object)
			{
				foreach (var rawBinDay in rawBinDays.EnumerateObject())
				{
					// Parse the date (e.g. "Tuesday 27 October 2026")
					var date = DateUtilities.ParseDateExact(rawBinDay.Value.GetProperty("collectiondate").GetString()!, "dddd dd MMMM yyyy");

					var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, rawBinDay.Value.GetProperty("bintype").GetString()!.Trim());

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
