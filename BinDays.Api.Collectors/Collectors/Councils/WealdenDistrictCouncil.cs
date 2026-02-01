namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

/// <summary>
/// Collector implementation for Wealden District Council.
/// </summary>
internal sealed class WealdenDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Wealden District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.wealden.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "wealden";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Refuse", "Rubbish" ],
			Type = BinType.Bin,
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling" ],
			Type = BinType.Bin,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden" ],
			Type = BinType.Bin,
		},
	];

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Remove spaces from postcode as the Wealden API requires postcodes without spaces in form data and URL parameters
		var sanitizedPostcode = postcode.Replace(" ", string.Empty);

		// Prepare client-side request for getting cookies
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://www.wealden.gov.uk/recycling-and-waste/bin-search/",
				Method = "GET",
				Headers = new()
				{
					{ "User-Agent", Constants.UserAgent },
				},
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
			clientSideResponse.Headers.TryGetValue("set-cookie", out var setCookieHeader);
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "action", "wealden_get_properties_in_postcode" },
				{ "postcode", sanitizedPostcode },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://www.wealden.gov.uk/wp-admin/admin-ajax.php",
				Method = "POST",
				Headers = new()
				{
					{ "Content-Type", "application/x-www-form-urlencoded; charset=UTF-8" },
					{ "X-Requested-With", "XMLHttpRequest" },
					{ "cookie", requestCookies },
					{ "User-Agent", Constants.UserAgent },
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
			var properties = jsonDoc.RootElement.GetProperty("properties").EnumerateArray();

			// Iterate through each property, and create a new address object
			var addresses = new List<Address>();
			foreach (var propertyElement in properties)
			{
				var address = new Address
				{
					Property = propertyElement.GetProperty("address").GetString()!.Trim(),
					Postcode = postcode,
					Uid = propertyElement.GetProperty("uprn").GetString()!,
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
		// Remove spaces from postcode as the Wealden API requires postcodes without spaces in URL parameters and cookies
		var sanitizedPostcode = (address.Postcode ?? string.Empty).Replace(" ", string.Empty);

		// Prepare client-side request for getting cookies
		if (clientSideResponse == null)
		{
			var requestUrl = $"https://www.wealden.gov.uk/recycling-and-waste/bin-search/?postcode={sanitizedPostcode}";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = requestUrl,
				Method = "GET",
				Headers = new()
				{
					{ "User-Agent", Constants.UserAgent },
				},
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
			clientSideResponse.Headers.TryGetValue("set-cookie", out var setCookieHeader);
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var cookies = string.IsNullOrWhiteSpace(requestCookies)
				? $"c_postcode={sanitizedPostcode}"
				: $"{requestCookies}; c_postcode={sanitizedPostcode}";

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "action", "wealden_get_collections_for_uprn" },
				{ "uprn", address.Uid! },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://www.wealden.gov.uk/wp-admin/admin-ajax.php",
				Method = "POST",
				Headers = new()
				{
					{ "Content-Type", "application/x-www-form-urlencoded; charset=UTF-8" },
					{ "X-Requested-With", "XMLHttpRequest" },
					{ "cookie", cookies },
					{ "User-Agent", Constants.UserAgent },
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
			var collection = jsonDoc.RootElement.GetProperty("collection");

			var binDays = new List<BinDay>();

			var binCollectionProperties = new Dictionary<string, string>
			{
				{ "refuseCollectionDate", "Refuse" },
				{ "recyclingCollectionDate", "Recycling" },
				{ "gardenCollectionDate", "Garden" },
			};

			// Iterate through each bin collection property, and create a new bin day object
			foreach (var property in binCollectionProperties)
			{
				if (!collection.TryGetProperty(property.Key, out var dateElement))
				{
					continue;
				}

				var dateString = dateElement.GetString();

				if (string.IsNullOrWhiteSpace(dateString))
				{
					continue;
				}

				var date = DateOnly.ParseExact(
					dateString,
					"yyyy-MM-dd'T'HH:mm:ss",
					CultureInfo.InvariantCulture,
					DateTimeStyles.None
				);

				var bins = ProcessingUtilities.GetMatchingBins(_binTypes, property.Value);

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = bins,
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
