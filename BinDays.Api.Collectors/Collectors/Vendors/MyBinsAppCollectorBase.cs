namespace BinDays.Api.Collectors.Collectors.Vendors;

using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

/// <summary>
/// Base collector implementation for councils using the MyBins App platform.
/// </summary>
internal abstract class MyBinsAppCollectorBase : GovUkCollectorBase
{
	/// <inheritdoc/>
	protected abstract int AuthorityId { get; }

	/// <inheritdoc/>
	protected abstract IReadOnlyCollection<Bin> BinTypes { get; }

	/// <summary>
	/// The base URL for the MyBins App API.
	/// </summary>
	private const string _apiBaseUrl = "https://api.mybinsapp.co.uk";

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var payload = new
			{
				authority_id = AuthorityId,
				query = postcode.Replace(" ", string.Empty).ToUpperInvariant()
			};

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_apiBaseUrl}/general/getAddresses",
				Method = "POST",
				Headers = new()
				{
					{ "content-type", "application/json" }
				},
				Body = JsonSerializer.Serialize(payload),
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
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
			var root = jsonDoc.RootElement;

			if (!root.GetProperty("success").GetBoolean())
			{
				throw new InvalidOperationException("API request failed");
			}

			var dataArray = root.GetProperty("dataArray");
			var addresses = new List<Address>();

			// Iterate through each address, and create a new address object
			foreach (var item in dataArray.EnumerateArray())
			{
				var address = new Address
				{
					Property = item.GetProperty("address_line").GetString()?.Trim(),
					Postcode = item.GetProperty("postcode").GetString()?.Trim(),
					Uid = item.GetProperty("address_id").GetString()?.Trim(),
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
			var today = DateOnly.FromDateTime(DateTime.Now);
			var fromDate = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
			var toDate = today.AddMonths(2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

			var payload = new
			{
				addressId = address.Uid,
				authority_id = AuthorityId,
				from = fromDate,
				to = toDate
			};

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_apiBaseUrl}/yourBin/getScheduler",
				Method = "POST",
				Headers = new()
				{
					{ "content-type", "application/json" }
				},
				Body = JsonSerializer.Serialize(payload),
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
			var root = jsonDoc.RootElement;

			if (!root.GetProperty("success").GetBoolean())
			{
				throw new InvalidOperationException("API request failed");
			}

			var dataArray = root.GetProperty("dataArray");
			var binDays = new List<BinDay>();

			// Iterate through each bin event, and create a new bin day object
			foreach (var item in dataArray.EnumerateArray())
			{
				var binName = item.GetProperty("title").GetString()?.Trim();
				if (string.IsNullOrEmpty(binName))
				{
					continue;
				}

				var startString = item.GetProperty("start").GetString();
				if (string.IsNullOrEmpty(startString))
				{
					continue;
				}

				// Parse ISO 8601 datetime (e.g. "2026-01-26T16:00:00+00:00")
				var date = DateOnly.FromDateTime(DateTime.Parse(startString, CultureInfo.InvariantCulture));

				var matchedBins = ProcessingUtilities.GetMatchingBins(BinTypes, binName);

				if (matchedBins.Count > 0)
				{
					binDays.Add(new BinDay
					{
						Date = date,
						Address = address,
						Bins = matchedBins
					});
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
