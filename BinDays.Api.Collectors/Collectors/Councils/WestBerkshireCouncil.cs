namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for West Berkshire Council.
/// </summary>
internal sealed partial class WestBerkshireCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "West Berkshire Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.westberks.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "west-berkshire";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Rubbish" ],
		},
		new()
		{
			Name = "Dry Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling" ],
			Type = BinType.Box,
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
	/// Regex for parsing JSONP responses.
	/// </summary>
	[GeneratedRegex(@"^[^(]+\((?<json>.*)\)$", RegexOptions.Singleline)]
	private static partial Regex JsonpRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

			var jsonPayload = $$"""
{
	"id": {{timestamp}},
	"method": "location.westberks.echoPostcodeFinderFILTERED",
	"params": {
		"provider": "",
		"postcode": "{{postcode}}"
	}
}
""";

			var url = $"https://www.westberks.gov.uk/apiserver/ajaxlibrary/?callback=jQuery{timestamp}&jsonrpc={Uri.EscapeDataString(jsonPayload)}&_={timestamp}";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = url,
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
			var contentMatch = JsonpRegex().Match(clientSideResponse.Content);
			var content = contentMatch.Groups["json"].Value;

			using var jsonDoc = JsonDocument.Parse(content);

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var addressElement in jsonDoc.RootElement.GetProperty("result").EnumerateArray())
			{
				var uprn = addressElement.GetProperty("udprn").GetString()!.Trim();
				var line1 = addressElement.GetProperty("line1").GetString()!.Trim();

				var address = new Address
				{
					Property = line1,
					Postcode = postcode,
					Uid = uprn,
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
			var requestBody = $$"""
{
	"jsonrpc": "2.0",
	"id": "1",
	"method": "goss.echo.westberks.forms.getNextRubbishRecyclingFoodCollectionDate3wkly",
	"params": {
		"uprn": "{{address.Uid}}"
	}
}
""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://www.westberks.gov.uk/apiserver/ajaxlibrary",
				Method = "POST",
				Headers = new()
				{
					{ "content-type", "application/json" },
					{ "user-agent", Constants.UserAgent },
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
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var result = jsonDoc.RootElement.GetProperty("result");

			var binDays = new List<BinDay>();

			void AddBinDay(string serviceName, string dateText)
			{
				var date = dateText.ParseDateInferringYear("dddd d MMMM");

				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, serviceName);

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = matchedBins,
				};

				binDays.Add(binDay);
			}

			var rubbishDate = result.GetProperty("nextRubbishDateText").GetString()!.Trim();

			if (!string.IsNullOrWhiteSpace(rubbishDate))
			{
				AddBinDay("Rubbish", rubbishDate);
			}

			var recyclingDate = result.GetProperty("nextRecyclingDateText").GetString()!.Trim();

			if (!string.IsNullOrWhiteSpace(recyclingDate))
			{
				AddBinDay("Recycling", recyclingDate);
			}

			var foodWasteDate = result.GetProperty("nextFoodWasteDateText").GetString()!.Trim();

			if (!string.IsNullOrWhiteSpace(foodWasteDate))
			{
				AddBinDay("Food", foodWasteDate);
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
