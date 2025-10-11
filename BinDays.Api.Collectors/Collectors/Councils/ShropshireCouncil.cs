namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Globalization;
	using System.Text.Json;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Collector implementation for Shropshire Council.
	/// </summary>
	internal sealed partial class ShropshireCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Shropshire Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://bins.shropshire.gov.uk/");

		/// <inheritdoc/>
		public override string GovUkId => "shropshire";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Mixed Recycling",
				Colour = BinColour.Purple,
				Keys = new List<string>() { "recycling" }.AsReadOnly(),
			},
			new()
			{
				Name = "Cardboard Recycling",
				Colour = BinColour.Blue,
				Keys = new List<string>() { "recycling" }.AsReadOnly(),
				Type = BinType.Bag,
			},
			new()
			{
				Name = "Garden & Food Waste",
				Colour = BinColour.Green,
				Keys = new List<string>() { "garden" }.AsReadOnly(),
			},
			new()
			{
				Name = "General Waste",
				Colour = BinColour.Black,
				Keys = new List<string>() { "general" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <summary>
		/// Regex for the addresses from the list elements.
		/// </summary>
		[GeneratedRegex(@"<li><a href=""/property/(?<Uprn>\d+)"">(?<Address>.*?)</li>")]
		private static partial Regex AddressesRegex();

		/// <summary>
		/// Regex for the bin days from the list elements.
		/// </summary>
		[GeneratedRegex(@"<li.*?title=""(?<Date>[^""]+?)\s*-\s*(?<CollectionType>[^""]+?)""")]
		private static partial Regex BinDaysRegex();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting cookies
			if (clientSideResponse == null)
			{
				// Prepare client-side request
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = "https://bins.shropshire.gov.uk/",
					Method = "GET",
					Headers = new Dictionary<string, string>() {
						{"user-agent", Constants.UserAgent},
					},
				};

				var getAddressesResponse = new GetAddressesResponse()
				{
					NextClientSideRequest = clientSideRequest
				};

				return getAddressesResponse;
			}
			// Prepare client-side request for getting addresses
			else if (clientSideResponse.RequestId == 1)
			{
				// Get set-cookies from response
				var setCookies = clientSideResponse.Headers["set-cookie"];
				var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookies);

				// Prepare client-side request
				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
				{
					{"aj", "true"},
					{"search_property", postcode},
				});

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = "https://bins.shropshire.gov.uk/property/",
					Method = "POST",
					Headers = new Dictionary<string, string>() {
						{"user-agent", Constants.UserAgent},
						{"content-type", "application/x-www-form-urlencoded"},
						{"cookie", requestCookies},
					},
					Body = requestBody,
				};

				var getAddressesResponse = new GetAddressesResponse()
				{
					NextClientSideRequest = clientSideRequest
				};

				return getAddressesResponse;
			}
			// Process addresses from response
			else if (clientSideResponse.RequestId == 2)
			{
				// Parse response content as JSON array
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

				// Get result property from json containing addresses html
				var result = jsonDoc.RootElement.GetProperty("result").GetString()!;

				// Get addresses from response
				var rawAddresses = AddressesRegex().Matches(result);

				// Iterate through each address json, and create a new address object
				var addresses = new List<Address>();
				foreach (Match rawAddress in rawAddresses)
				{
					var uid = rawAddress.Groups["Uprn"].Value;

					var address = new Address()
					{
						Property = rawAddress.Groups["Address"].Value,
						Postcode = postcode,
						Uid = uid,
					};

					addresses.Add(address);
				}

				var getAddressesResponse = new GetAddressesResponse()
				{
					Addresses = addresses.AsReadOnly(),
				};

				return getAddressesResponse;
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}

		/// <inheritdoc/>
		public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting cookies
			if (clientSideResponse == null)
			{
				// Prepare client-side request
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = "https://bins.shropshire.gov.uk/",
					Method = "GET",
					Headers = new Dictionary<string, string>() {
						{"user-agent", Constants.UserAgent},
					},
				};

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Prepare client-side request for getting bin days
			if (clientSideResponse.RequestId == 1)
			{
				// Get set-cookies from response
				var setCookies = clientSideResponse.Headers["set-cookie"];
				var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookies);

				var requestUrl = $"https://bins.shropshire.gov.uk/property/{address.Uid}";

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = requestUrl,
					Method = "GET",
					Headers = new Dictionary<string, string>() {
						{"user-agent", Constants.UserAgent},
						{"cookie", requestCookies},
					},
				};

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Process bin days from response
			else if (clientSideResponse.RequestId == 2)
			{
				// Get bin days from response
				var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content)!;

				// Iterate through each bin day, and create a new bin day object
				var binDays = new List<BinDay>();
				foreach (Match rawBinDay in rawBinDays)
				{
					var dateString = rawBinDay.Groups["Date"].Value;
					var collectionType = rawBinDay.Groups["CollectionType"].Value;

					// Parse the date (e.g. 'Friday 4, April 2025')
					var date = DateOnly.ParseExact(
						dateString,
						"dddd d, MMMM yyyy",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);

					// Skip bin day if in the past
					if (date < DateOnly.FromDateTime(DateTime.Now))
					{
						continue;
					}

					// Get matching bin types from the type using the keys
					var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, collectionType);

					var binDay = new BinDay()
					{
						Date = date,
						Address = address,
						Bins = matchedBinTypes,
					};

					binDays.Add(binDay);
				}

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = ProcessingUtilities.ProcessBinDays(binDays),
				};

				return getBinDaysResponse;
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}
	}
}
