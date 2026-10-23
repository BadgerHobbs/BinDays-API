namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

/// <summary>
/// Collector implementation for Rochdale Metropolitan Borough Council.
/// </summary>
internal sealed class RochdaleMetropolitanBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Rochdale Metropolitan Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.rochdale.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "rochdale";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "General Waste" ],
		},
		new()
		{
			Name = "Glass, Cans & Plastics",
			Colour = BinColour.LightGreen,
			Keys = [ "Glass and Bottles" ],
		},
		new()
		{
			Name = "Paper & Cardboard",
			Colour = BinColour.Blue,
			Keys = [ "Paper and Cardboard" ],
		},
		new()
		{
			Name = "Food & Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Food and Garden" ],
		},
	];

	/// <summary>
	/// The URL of the council's bin collection calendar form, used to start the session.
	/// </summary>
	private const string _sessionUrl = "https://rochdale-self.achieveservice.com/service/Bins___view_your_waste_collection_calendar";

	/// <summary>
	/// The URL of the council's Achieve Forms lookups.
	/// </summary>
	private const string _lookupUrl = "https://rochdale-self.achieveservice.com/apibroker/runLookup";

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
				Url = $"{_lookupUrl}?id=689de2aea6584",
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
				Url = $"{_lookupUrl}?id=6846c784a46b5",
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
				.GetProperty("bartecToken")
				.GetString()!;

			// The council website requests the dates for the next three weeks, so ask for eight weeks to show more
			var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Europe/London"));
			var minDate = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
			var maxDate = today.AddDays(56).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

			var requestBody = $$"""
			{
				"formValues": {
					"Section 1": {
						"propertyUPRN": { "value": "{{address.Uid}}" },
						"bartecToken": { "value": "{{token}}" },
						"dateMinimum": { "value": "{{minDate}}" },
						"dateMaximum": { "value": "{{maxDate}}" }
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"{_lookupUrl}?id=686e9147a867e",
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
					var date = DateUtilities.ParseDateExact(
						rawBinDay.Value.GetProperty("bartecBinStartDate").GetString()!,
						"yyyy-MM-dd'T'HH:mm:ss"
					);

					var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, rawBinDay.Value.GetProperty("bartecBinType").GetString()!);

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
