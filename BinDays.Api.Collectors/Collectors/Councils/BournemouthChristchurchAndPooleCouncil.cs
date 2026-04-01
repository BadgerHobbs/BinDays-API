namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Collector implementation for Bournemouth, Christchurch and Poole Council.
/// </summary>
internal sealed class BournemouthChristchurchAndPooleCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Bournemouth, Christchurch and Poole Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://bcpportal.bcpcouncil.gov.uk/checkyourbincollection/");

	/// <inheritdoc/>
	public override string GovUkId => "bournemouth-christchurch-poole";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes = [
		new()
		{
			Name = "Rubbish",
			Colour = BinColour.Black,
			Keys = [ "Rubbish" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "Food waste" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Garden Waste" ],
		},
	];

	/// <summary>
	/// Used for the Address API call.
	/// </summary>
	private const string _apiKey = "f5a8f110545e4d009411c908b25b7596";

	/// <summary>
	/// Used for the Bin Day API call
	/// </summary>
	private const string _signature = "TAvYIUFj6dzaP90XQCm2ElY6Cd34ze05I3ba7LKTiBs";

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var requestUrl = $"https://apim-uks-cepprod-int-01.azure-api.net/LLPGSearch?searchText={postcode}&Subscription-Key={_apiKey}";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = requestUrl,
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 1)
		{
			// Parse response content as JSON array
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

			var rootElement = jsonDoc.RootElement;
			JsonElement resultsElement;
			if (rootElement.ValueKind == JsonValueKind.Array)
			{
				resultsElement = rootElement;
			}
			else if (rootElement.TryGetProperty("Results", out var upperCaseResultsElement))
			{
				resultsElement = upperCaseResultsElement;
			}
			else if (rootElement.TryGetProperty("results", out var lowerCaseResultsElement))
			{
				resultsElement = lowerCaseResultsElement;
			}
			else
			{
				throw new KeyNotFoundException("Results");
			}

			// Iterate through each address json, and create a new address object
			var addresses = new List<Address>();
			foreach (var addressElement in resultsElement.EnumerateArray())
			{
				string property;
				if (addressElement.TryGetProperty("FULL_ADDRESS", out var fullAddressElement))
				{
					property = fullAddressElement.GetString()!.Trim();
				}
				else if (addressElement.TryGetProperty("fullAddress", out var fullAddressLowerCaseElement))
				{
					property = fullAddressLowerCaseElement.GetString()!.Trim();
				}
				else
				{
					throw new KeyNotFoundException("FULL_ADDRESS");
				}

				string uid;
				if (addressElement.TryGetProperty("UPRN", out var uprnElement))
				{
					uid = uprnElement.GetString()!.Trim();
				}
				else if (addressElement.TryGetProperty("uprn", out var uprnLowerCaseElement))
				{
					uid = uprnLowerCaseElement.GetString()!.Trim();
				}
				else
				{
					throw new KeyNotFoundException("UPRN");
				}

				var address = new Address
				{
					Property = property,
					Postcode = postcode,
					Uid = uid,
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
			var requestUrl = $"https://prod-17.uksouth.logic.azure.com/workflows/58253d7b7d754447acf9fe5fcf76f493/triggers/manual/paths/invoke?api-version=2016-06-01&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig={_signature}";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = requestUrl,
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.ApplicationJson },
				},
				Body = JsonSerializer.Serialize(new { uprn = address.Uid }),
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

			var binDays = new List<BinDay>();
			if (jsonDoc.RootElement.TryGetProperty("data", out var resultsElement))
			{
				foreach (var binTypeElement in resultsElement.EnumerateArray())
				{
					// Determine matching bin types from the description
					var description = binTypeElement.GetProperty("wasteContainerUsageTypeDescription").GetString()!;
					var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, description);

					var rangeEl = binTypeElement.GetProperty("scheduleDateRange");
					foreach (var dateEl in rangeEl.EnumerateArray())
					{
						var date = DateUtilities.ParseDateExact(dateEl.GetString()!, "yyyy-MM-dd");

						var binDay = new BinDay
						{
							Date = date,
							Address = address,
							Bins = matchedBinTypes,
						};

						binDays.Add(binDay);
					}
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
