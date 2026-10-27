namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
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
			var clientSideRequest = CreateAddressLookupRequest(1, postcode);

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 1)
		{
			if (!IsJsonResponse(clientSideResponse))
			{
				var clientSideRequest = CreateAddressLookupRequest(2, postcode);

				var getAddressesResponse = new GetAddressesResponse
				{
					NextClientSideRequest = clientSideRequest,
				};

				return getAddressesResponse;
			}

			var parsedAddressesResponse = ParseAddressesResponse(postcode, clientSideResponse.Content);

			return parsedAddressesResponse;
		}
		else if (clientSideResponse.RequestId == 2)
		{
			var parsedAddressesResponse = ParseAddressesResponse(postcode, clientSideResponse.Content);

			return parsedAddressesResponse;
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
			var clientSideRequest = CreateBinDaysRequest(1, address.Uid!);

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 1)
		{
			if (!IsJsonResponse(clientSideResponse))
			{
				var clientSideRequest = CreateBinDaysRequest(2, address.Uid!);

				var getBinDaysResponse = new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest,
				};

				return getBinDaysResponse;
			}

			var parsedBinDaysResponse = ParseBinDaysResponse(address, clientSideResponse.Content);

			return parsedBinDaysResponse;
		}
		else if (clientSideResponse.RequestId == 2)
		{
			var parsedBinDaysResponse = ParseBinDaysResponse(address, clientSideResponse.Content);

			return parsedBinDaysResponse;
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <summary>
	/// Creates the address lookup request for the given postcode.
	/// </summary>
	/// <param name="requestId">The request identifier.</param>
	/// <param name="postcode">The postcode to look up.</param>
	/// <returns>The client-side request for address lookup.</returns>
	private static ClientSideRequest CreateAddressLookupRequest(int requestId, string postcode)
	{
		var clientSideRequest = new ClientSideRequest
		{
			RequestId = requestId,
			Url = $"https://www.enfield.gov.uk/_design/integrations/ordnance-survey/places-v2?query={postcode}",
			Method = "GET",
		};

		return clientSideRequest;
	}

	/// <summary>
	/// Creates the bin days lookup request for the given UPRN.
	/// </summary>
	/// <param name="requestId">The request identifier.</param>
	/// <param name="uprn">The UPRN for the selected address.</param>
	/// <returns>The client-side request for bin day lookup.</returns>
	private static ClientSideRequest CreateBinDaysRequest(int requestId, string uprn)
	{
		var requestUrl = $"https://www.enfield.gov.uk/_design/integrations/bartec/find-my-collection/rest/schedule?uprn={uprn}";

		var clientSideRequest = new ClientSideRequest
		{
			RequestId = requestId,
			Url = requestUrl,
			Method = "GET",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "referer", "https://www.enfield.gov.uk/services/rubbish-and-recycling/find-my-collection-day" },
			},
		};

		return clientSideRequest;
	}

	/// <summary>
	/// Determines whether the response contains JSON content.
	/// </summary>
	/// <param name="clientSideResponse">The client-side response to inspect.</param>
	/// <returns><c>true</c> if the response appears to be JSON; otherwise <c>false</c>.</returns>
	private static bool IsJsonResponse(ClientSideResponse clientSideResponse)
	{
		if (!clientSideResponse.Headers.TryGetValue("content-type", out var contentType))
		{
			return false;
		}

		if (!contentType.Contains(Constants.ApplicationJson, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		if (string.IsNullOrWhiteSpace(clientSideResponse.Content))
		{
			return false;
		}

		var trimmedContent = clientSideResponse.Content.TrimStart();
		return trimmedContent.StartsWith("{", StringComparison.Ordinal) || trimmedContent.StartsWith("[", StringComparison.Ordinal);
	}

	/// <summary>
	/// Parses the address lookup response into address results.
	/// </summary>
	/// <param name="postcode">The postcode used for lookup.</param>
	/// <param name="content">The raw JSON response content.</param>
	/// <returns>The parsed get addresses response.</returns>
	private static GetAddressesResponse ParseAddressesResponse(string postcode, string content)
	{
		using var jsonDoc = JsonDocument.Parse(content);
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

	/// <summary>
	/// Parses the bin day lookup response into bin day results.
	/// </summary>
	/// <param name="address">The selected address.</param>
	/// <param name="content">The raw JSON response content.</param>
	/// <returns>The parsed get bin days response.</returns>
	private GetBinDaysResponse ParseBinDaysResponse(Address address, string content)
	{
		using var jsonDoc = JsonDocument.Parse(content);

		// Iterate through each bin day, and create a new bin day object
		var binDays = new List<BinDay>();
		foreach (var jobElement in jsonDoc.RootElement.EnumerateArray())
		{
			var service = jobElement.GetProperty("JobName").GetProperty("_text").GetString()!.Trim();
			var scheduledStart = jobElement.GetProperty("ScheduledStart").GetProperty("_text").GetString()!.Trim();
			var parsedDate = DateUtilities.ParseDateExact(scheduledStart[..10], "yyyy-MM-dd");

			var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

			var binDay = new BinDay
			{
				Date = parsedDate,
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
}
