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
internal sealed partial class WealdenDistrictCouncil : GovUkCollectorBase, ICollector
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
		var formattedPostcode = ProcessingUtilities.FormatPostcode(postcode) ?? string.Empty;
		var sanitizedPostcode = formattedPostcode.Replace(" ", string.Empty);

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
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
				clientSideResponse.Headers["set-cookie"]);

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

			var addresses = new List<Address>();
			foreach (var propertyElement in properties)
			{
				var address = new Address
				{
					Property = propertyElement.GetProperty("address").GetString()!.Trim(),
					Postcode = formattedPostcode,
					Uid = propertyElement.GetProperty("uprn").GetString(),
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
		var formattedPostcode = ProcessingUtilities.FormatPostcode(address.Postcode ?? string.Empty) ?? string.Empty;
		var sanitizedPostcode = formattedPostcode.Replace(" ", string.Empty);

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
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
				clientSideResponse.Headers["set-cookie"]);

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

			AddBinDay(collection, "refuseCollectionDate", "Refuse", address, binDays);
			AddBinDay(collection, "recyclingCollectionDate", "Recycling", address, binDays);
			AddBinDay(collection, "gardenCollectionDate", "Garden", address, binDays);

			var getBinDaysResponse = new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};

			return getBinDaysResponse;
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}

	private void AddBinDay(JsonElement collection, string propertyName, string service, Address address, List<BinDay> binDays)
	{
		if (!collection.TryGetProperty(propertyName, out var dateElement))
		{
			return;
		}

		var dateString = dateElement.GetString();

		if (string.IsNullOrWhiteSpace(dateString))
		{
			return;
		}

		var date = DateOnly.ParseExact(
			dateString,
			"yyyy-MM-dd'T'HH:mm:ss",
			CultureInfo.InvariantCulture,
			DateTimeStyles.None
		);

		var bins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

		var binDay = new BinDay
		{
			Date = date,
			Address = address,
			Bins = bins,
		};

		binDays.Add(binDay);
	}
}
