namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

/// <summary>
/// Collector implementation for Chesterfield Borough Council.
/// </summary>
internal sealed class ChesterfieldBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Chesterfield Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.chesterfield.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "chesterfield";

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
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "Food waste" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Garden waste" ],
		},
	];

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var parameters = $$"""
			{
				"postCode": "{{postcode}}"
			}
			""";

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = CreateActionRequest("getAddressListFromPostCode", parameters),
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 1)
		{
			// The addresses are returned as a JSON string within the JSON response
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			using var addressesJsonDoc = JsonDocument.Parse(GetReturnValue(jsonDoc).GetString()!);
			var rawAddresses = addressesJsonDoc.RootElement.EnumerateArray();

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in rawAddresses)
			{
				var address = new Address
				{
					Property = rawAddress.GetProperty("label").GetString()!.Trim(),
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
		// Prepare client-side request for getting bin days
		if (clientSideResponse == null)
		{
			var parameters = $$"""
			{
				"propertyUprn": "{{address.Uid}}",
				"executedFrom": "Main Website"
			}
			""";

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = CreateActionRequest("getServicesByUPRN", parameters),
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var rawServices = GetReturnValue(jsonDoc).GetProperty("reducedServices").EnumerateArray();

			// Iterate through each service, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var rawService in rawServices)
			{
				// Services without a scheduled collection (e.g. garden waste without a subscription) have no task or next instance
				if (!rawService.TryGetProperty("firstServiceTask", out var rawTask) || !rawTask.TryGetProperty("nextInstance", out var rawNextInstance))
				{
					continue;
				}

				var service = rawService.GetProperty("cbcFriendlyServiceName").GetString()!;

				// Dates are in UTC, as midnight UK time (e.g. "2026-10-18T23:00:00.000Z" for 19 October)
				var utcDate = DateTime.ParseExact(
					rawNextInstance.GetProperty("currentScheduledDate").GetString()!,
					"yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
					CultureInfo.InvariantCulture,
					DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
				);
				var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(utcDate, "Europe/London"));

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
	/// Creates the client-side request for calling a method of the council's collection days component.
	/// </summary>
	private static ClientSideRequest CreateActionRequest(string method, string parameters)
	{
		var message = $$"""
		{
			"actions": [
				{
					"descriptor": "aura://ApexActionController/ACTION$execute",
					"params": {
						"classname": "CBC_VE_CollectionDays",
						"method": "{{method}}",
						"params": {{parameters}}
					}
				}
			]
		}
		""";

		var context = """
		{
			"mode": "PROD",
			"app": "c:cbc_VE_CollectionDaysLO"
		}
		""";

		var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
		{
			{ "message", message },
			{ "aura.context", context },
			{ "aura.token", "null" },
		});

		var clientSideRequest = new ClientSideRequest
		{
			RequestId = 1,
			Url = "https://myaccount.chesterfield.gov.uk/anonymous/aura",
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", Constants.FormUrlEncoded },
			},
			Body = requestBody,
		};

		return clientSideRequest;
	}

	/// <summary>
	/// Gets the value returned by the method from the response.
	/// </summary>
	private static JsonElement GetReturnValue(JsonDocument jsonDoc)
	{
		var returnValue = jsonDoc.RootElement.GetProperty("actions")[0].GetProperty("returnValue").GetProperty("returnValue");

		return returnValue;
	}
}
