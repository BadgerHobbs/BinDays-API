namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors.Vendors;
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Globalization;
	using System.Net;
	using System.Text.Json;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Collector implementation for Broadland District Council.
	/// </summary>
	internal sealed partial class BroadlandDistrictCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Broadland District Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://area.southnorfolkandbroadland.gov.uk/FindAddress");

		/// <inheritdoc/>
		public override string GovUkId => "broadland";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly IReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Rubbish",
				Colour = BinColour.Green,
				Keys = ["Rubbish"],
			},
			new()
			{
				Name = "Recycling",
				Colour = BinColour.Grey,
				Keys = ["Recycling"],
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Brown,
				Keys = ["Garden"],
			},
			new()
			{
				Name = "Food Waste",
				Colour = BinColour.Black,
				Keys = ["Food"],
				Type = BinType.Caddy
			},
		}.AsReadOnly();

		/// <summary>
		/// Regex for the RequestVerificationToken.
		/// </summary>
		[GeneratedRegex(@"<input name=""__RequestVerificationToken"" type=""hidden"" value=""(?<token>[^""]+)"" />")]
		private static partial Regex TokenRegex();

		/// <summary>
		/// Regex for parsing addresses from the options elements.
		/// </summary>
		[GeneratedRegex(@"<option value=""(?<value>[^""]+)"">(?<address>[^<]+)</option>")]
		private static partial Regex AddressesRegex();

		/// <summary>
		/// Regex for parsing the ReCollect Place ID from the dashboard HTML.
		/// </summary>
		[GeneratedRegex(@"api\.eu\.recollect\.net/api/places/(?<placeId>[a-zA-Z0-9-]+)/services")]
		private static partial Regex PlaceIdRegex();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting token
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = "https://area.southnorfolkandbroadland.gov.uk/FindAddress",
					Method = "GET",
					Headers = new() {
						{"User-Agent", Constants.UserAgent},
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
				// Extract token and cookie
				var token = TokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;
				var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);

				// Prepare request body
				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{ "Postcode", postcode },
					{ "__RequestVerificationToken", token }
				});

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 2,
					Url = "https://area.southnorfolkandbroadland.gov.uk/FindAddress",
					Method = "POST",
					Headers = new() {
						{"User-Agent", Constants.UserAgent},
						{"Content-Type", "application/x-www-form-urlencoded"},
						{"Cookie", requestCookies},
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
				var addresses = new List<Address>();
				var matches = AddressesRegex().Matches(clientSideResponse.Content);

				foreach (Match match in matches)
				{
					var value = match.Groups["value"].Value;
					var displayText = match.Groups["address"].Value;

					// Skip the "X addresses found" option which has an empty value usually,
					// but regex ensures value is captured. 
					if (string.IsNullOrWhiteSpace(value))
					{
						continue;
					}

					// The value contains all data needed for the cookie, separated by semicolons.
					// Format: UPRN;Address;X;Y;Ward;Parish;Village;Street;Authority
					// We store this entire string as the Uid to reconstruct the cookie later.
					var address = new Address
					{
						Property = displayText.Trim(),
						Postcode = postcode,
						Uid = value,
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
			// Prepare client-side request for getting dashboard (to extract ReCollect ID)
			if (clientSideResponse == null)
			{
				// Reconstruct the MyArea.Data cookie
				// Value format: UPRN;Address;X;Y;Ward;Parish;Village;Street;Authority
				var parts = address.Uid!.Split(';');

				var cookieData = new
				{
					Uprn = parts[0],
					Address = parts[1],
					X = parts[2],
					Y = parts[3],
					Ward = parts[4],
					Parish = parts[5],
					Village = parts[6],
					Street = parts[7],
					Authority = parts[8]
				};

				var jsonString = JsonSerializer.Serialize(cookieData);
				var encodedJson = WebUtility.UrlEncode(jsonString);

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = "https://area.southnorfolkandbroadland.gov.uk/",
					Method = "GET",
					Headers = new() {
						{"User-Agent", Constants.UserAgent},
						{"Cookie", $"MyArea.Data={encodedJson}"},
					},
				};

				return new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest
				};
			}
			// Process dashboard response to find API Place ID and make API request
			else if (clientSideResponse.RequestId == 1)
			{
				var placeId = PlaceIdRegex().Match(clientSideResponse.Content).Groups["placeId"].Value;
				var dateFrom = DateTime.Now.ToString("yyyy-MM-dd");
				var dateTo = DateTime.Now.AddYears(1).ToString("yyyy-MM-dd");

				// Construct ReCollect API URL
				var requestUrl = $"https://api.eu.recollect.net/api/places/{placeId}/services/6/events?nomerge=true&locale=en-GB&after={dateFrom}&before={dateTo}";

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 2,
					Url = requestUrl,
					Method = "GET",
					Headers = new() {
						{"User-Agent", Constants.UserAgent},
					},
				};

				return new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest
				};
			}
			// Process ReCollect API response
			else if (clientSideResponse.RequestId == 2)
			{
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
				var eventsElement = jsonDoc.RootElement.GetProperty("events");

				var binDays = new List<BinDay>();

				foreach (var eventElement in eventsElement.EnumerateArray())
				{
					var dateString = eventElement.GetProperty("day").GetString()!;

					// Parse date string (e.g. 2025-03-01)
					var date = DateOnly.ParseExact(
						dateString,
						"yyyy-MM-dd",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);

					var flagsElement = eventElement.GetProperty("flags");
					foreach (var flag in flagsElement.EnumerateArray())
					{
						var subject = flag.GetProperty("subject").GetString()!;
						var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, subject);

						var binDay = new BinDay
						{
							Date = date,
							Address = address,
							Bins = matchedBins,
						};

						binDays.Add(binDay);
					}
				}

				return new GetBinDaysResponse
				{
					BinDays = ProcessingUtilities.ProcessBinDays(binDays),
				};
			}

			throw new InvalidOperationException("Invalid client-side request.");
		}
	}
}
