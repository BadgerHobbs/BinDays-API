namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Durham County Council.
/// </summary>
internal sealed partial class DurhamCountyCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Durham County Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.durham.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "county-durham";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "Empty Bin Ref" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Empty Bin Rec" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Black,
			Keys = [ "Empty Bin Foo" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Empty Bin Org" ],
		},
	];

	/// <summary>
	/// Regex for the addresses from the data.
	/// </summary>
	[GeneratedRegex(@"<uprn>(?<uid>\d+)</uprn>\s*<formatted_address>(?<address>[^<]+)</formatted_address>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the bin days from the data.
	/// </summary>
	[GeneratedRegex(@"<Job>[\s\S]*?<Name[^>]*>(?<service>[^<]+)</Name>[\s\S]*?<ScheduledStart>(?<date>\d{4}-\d{2}-\d{2})T[^<]+</ScheduledStart></Job>")]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = CreateMethodRequest("PostcodeLookup", "postcode", postcode),
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 1)
		{
			var rawAddresses = AddressRegex().Matches(GetResult(clientSideResponse))!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var address = new Address
				{
					Property = rawAddress.Groups["address"].Value.Trim(),
					Postcode = postcode,
					Uid = rawAddress.Groups["uid"].Value,
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
			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = CreateMethodRequest("GetBartecCalendar", "uprn", address.Uid!),
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 1)
		{
			var rawBinDays = BinDaysRegex().Matches(GetResult(clientSideResponse))!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["service"].Value;
				var date = DateUtilities.ParseDateExact(rawBinDay.Groups["date"].Value, "yyyy-MM-dd");

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				var binDay = new BinDay
				{
					Date = date,
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

	/// <summary>
	/// Creates the client-side request for calling a method of the council's localities service with a single parameter.
	/// </summary>
	private static ClientSideRequest CreateMethodRequest(string method, string parameter, string value)
	{
		var requestBody = $$"""
		{
			"jsonrpc": "2.0",
			"method": "durham.Localities.{{method}}",
			"params": {
				"{{parameter}}": "{{value}}"
			},
			"id": "1"
		}
		""";

		var clientSideRequest = new ClientSideRequest
		{
			RequestId = 1,
			Url = "https://www.durham.gov.uk/apiserver/ajaxlibrary/",
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", Constants.ApplicationJson },
			},
			Body = requestBody,
		};

		return clientSideRequest;
	}

	/// <summary>
	/// Gets the result of the method from the response, which is XML returned as a JSON string.
	/// </summary>
	private static string GetResult(ClientSideResponse clientSideResponse)
	{
		using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
		var result = jsonDoc.RootElement.GetProperty("result").GetString()!;

		return result;
	}
}
