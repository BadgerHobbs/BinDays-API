namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Hartlepool Borough Council.
/// </summary>
internal sealed partial class HartlepoolBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Hartlepool Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.hartlepool.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "hartlepool";

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
			Name = "Mixed Recycling",
			Colour = BinColour.Grey,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Black,
			Keys = [ "Food Waste" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden Waste" ],
		},
	];

	/// <summary>
	/// The URL of the check bin day service, used to start a session.
	/// </summary>
	private const string _sessionUrl = "https://online.hartlepool.gov.uk/service/Refuse_and_recycling___check_bin_day";

	/// <summary>
	/// The URL of the AchieveForms lookup endpoint.
	/// </summary>
	private const string _lookupUrl = "https://online.hartlepool.gov.uk/apibroker/runLookup";

	/// <summary>
	/// Regex for the service and date of each collection, e.g. "General Waste (green) 27/10/2026".
	/// </summary>
	[GeneratedRegex(@"<span[^>]*>\s*(?<service>[^<]+?) (?<date>\d{2}/\d{2}/\d{4})</span>")]
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
				Url = $"{_lookupUrl}?id=587f3deed5d4b",
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
		// Prepare client-side request for getting bin days
		else if (clientSideResponse.RequestId == 1)
		{
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);

			var requestBody = $$"""
			{
				"formValues": {
					"Section 1": {
						"collectionLocationUPRN": { "value": "{{address.Uid}}" }
					}
				}
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_lookupUrl}?id=5ec67e019ffdd",
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
		else if (clientSideResponse.RequestId == 2)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var collectionsHtml = jsonDoc.RootElement
				.GetProperty("integration")
				.GetProperty("transformed")
				.GetProperty("rows_data")
				.GetProperty("0")
				.GetProperty("HTMLCollectionDatesText")
				.GetString()!;

			var rawBinDays = BinDayRegex().Matches(collectionsHtml)!;

			// Iterate through each collection, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["service"].Value;
				var date = DateUtilities.ParseDateExact(rawBinDay.Groups["date"].Value, "dd/MM/yyyy");

				var binDay = new BinDay
				{
					Date = date,
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

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}
}
