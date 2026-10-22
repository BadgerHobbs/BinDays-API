namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Collector implementation for Leicester City Council.
/// </summary>
internal sealed class LeicesterCityCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Leicester City Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.leicester.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "leicester";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Refuse" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Orange,
			Keys = [ "Recycling" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Green Waste" ],
		},
	];

	/// <summary>
	/// The URL of the Biffa Leicester lookup endpoint.
	/// </summary>
	private const string _apiUrl = "https://biffaleicester.co.uk/wp-admin/admin-ajax.php";

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _apiUrl,
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{ "action", "get_uprn_api" },
					{ "postcode", postcode },
				}),
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
			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var result in GetResults(clientSideResponse.Content))
			{
				var address = new Address
				{
					Property = result.GetProperty("UPRNAddress").GetString()!.Trim(),
					Postcode = postcode,
					Uid = result.GetProperty("UPRNID").GetString()!,
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
				Url = _apiUrl,
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{ "action", "get_details_api" },
					{ "uprn", address.Uid! },
				}),
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
			// Iterate through each service, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var result in GetResults(clientSideResponse.Content))
			{
				var service = result.GetProperty("ServiceModeDesc").GetString()!.Trim();
				var date = DateUtilities.ParseDateExact(result.GetProperty("ServiceDueDate").GetString()!, "dd/MM/yy");

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

	/// <summary>
	/// Gets the results from a lookup response.
	/// </summary>
	/// <remarks>
	/// The lookup returns an empty object when there are no results, and a single object rather than an array when there is one result.
	/// </remarks>
	private static List<JsonElement> GetResults(string content)
	{
		using var jsonDoc = JsonDocument.Parse(content);

		var results = new List<JsonElement>();
		if (!jsonDoc.RootElement.TryGetProperty("anyType", out var anyType))
		{
			return results;
		}

		if (anyType.ValueKind == JsonValueKind.Array)
		{
			foreach (var result in anyType.EnumerateArray())
			{
				results.Add(result.Clone());
			}
		}
		else
		{
			results.Add(anyType.Clone());
		}

		return results;
	}
}
