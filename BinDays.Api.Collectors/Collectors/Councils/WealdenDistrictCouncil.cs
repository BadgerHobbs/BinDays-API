namespace BinDays.Api.Collectors.Collectors.Councils
{
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
		private readonly IReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "General Waste",
				Colour = BinColour.Black,
				Keys = new List<string>() { "Refuse", "Rubbish" }.AsReadOnly(),
				Type = BinType.Bin,
			},
			new()
			{
				Name = "Recycling",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Recycling" }.AsReadOnly(),
				Type = BinType.Bin,
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Brown,
				Keys = new List<string>() { "Garden" }.AsReadOnly(),
				Type = BinType.Bin,
			},
		}.AsReadOnly();

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

				return new GetAddressesResponse
				{
					NextClientSideRequest = clientSideRequest
				};
			}
			// Prepare client-side request for getting addresses
			else if (clientSideResponse.RequestId == 1)
			{
				var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
					clientSideResponse.Headers["set-cookie"]);

				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{ "action", "wealden_get_properties_in_postcode" },
					{ "postcode", sanitizedPostcode }
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

				return new GetAddressesResponse
				{
					NextClientSideRequest = clientSideRequest
				};
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

				return new GetAddressesResponse
				{
					Addresses = addresses.AsReadOnly(),
				};
			}

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

				return new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest
				};
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
					{ "uprn", address.Uid! }
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

				return new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest
				};
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

				return new GetBinDaysResponse
				{
					BinDays = ProcessingUtilities.ProcessBinDays(binDays),
				};
			}

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

			var date = DateOnly.ParseExact(dateString, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

			var bins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

			binDays.Add(new BinDay
			{
				Date = date,
				Address = address,
				Bins = bins
			});
		}
	}
}
