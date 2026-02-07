namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

/// <summary>
/// Collector implementation for London Borough of Enfield.
/// </summary>
internal sealed class LondonBoroughOfEnfield : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "London Borough of Enfield";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.enfield.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "enfield";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Residual" ],
		},
		new()
		{
			Name = "Mixed Recycling Bin",
			Colour = BinColour.Blue,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Food Waste Caddy",
			Colour = BinColour.Brown,
			Keys = [ "Food" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste Bin",
			Colour = BinColour.Green,
			Keys = [ "Garden" ],
		},
	];

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://www.enfield.gov.uk/services/rubbish-and-recycling/find-my-collection-day",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8" },
					{ "sec-ch-ua", "\"Not A(Brand\";v=\"99\", \"Mozilla\";v=\"137\", \"Chromium\";v=\"137\"" },
					{ "sec-ch-ua-mobile", "?0" },
					{ "sec-ch-ua-platform", "\"Windows\"" },
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
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"https://www.enfield.gov.uk/_design/integrations/ordnance-survey/places-v2?query={postcode}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "accept", "*/*" },
					{ "referer", "https://www.enfield.gov.uk/services/rubbish-and-recycling/find-my-collection-day" },
					{ "sec-fetch-dest", "empty" },
					{ "sec-fetch-mode", "cors" },
					{ "sec-fetch-site", "same-origin" },
					{ "cookie", requestCookies },
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		else if (clientSideResponse.RequestId == 2)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var resultsElement = jsonDoc.RootElement.GetProperty("results");

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var resultElement in resultsElement.EnumerateArray())
			{
				var lpiElement = resultElement.GetProperty("LPI");

				var address = new Address
				{
					Property = lpiElement.GetProperty("ADDRESS").GetString()!.Trim(),
					Postcode = postcode,
					Uid = lpiElement.GetProperty("UPRN").GetString()!.Trim(),
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
		// Prepare client-side request for getting bin days
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://www.enfield.gov.uk/services/rubbish-and-recycling/find-my-collection-day",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8" },
					{ "sec-ch-ua", "\"Not A(Brand\";v=\"99\", \"Mozilla\";v=\"137\", \"Chromium\";v=\"137\"" },
					{ "sec-ch-ua-mobile", "?0" },
					{ "sec-ch-ua-platform", "\"Windows\"" },
				},
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
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);
			var requestUrl = $"https://www.enfield.gov.uk/_design/integrations/bartec/find-my-collection/rest/schedule?uprn={address.Uid!}";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = requestUrl,
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "accept", "*/*" },
					{ "referer", "https://www.enfield.gov.uk/services/rubbish-and-recycling/find-my-collection-day" },
					{ "sec-fetch-dest", "empty" },
					{ "sec-fetch-mode", "cors" },
					{ "sec-fetch-site", "same-origin" },
					{ "cookie", requestCookies },
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		else if (clientSideResponse.RequestId == 2)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var jobElement in jsonDoc.RootElement.EnumerateArray())
			{
				var service = jobElement.GetProperty("JobName").GetProperty("_text").GetString()!.Trim();
				var scheduledStart = jobElement.GetProperty("ScheduledStart").GetProperty("_text").GetString()!.Trim();

				var parsedDate = DateTime.ParseExact(
					scheduledStart,
					"yyyy-MM-dd'T'HH:mm:ss",
					CultureInfo.InvariantCulture,
					DateTimeStyles.None
				);

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				var binDay = new BinDay
				{
					Date = DateOnly.FromDateTime(parsedDate),
					Address = address,
					Bins = matchedBinTypes,
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
