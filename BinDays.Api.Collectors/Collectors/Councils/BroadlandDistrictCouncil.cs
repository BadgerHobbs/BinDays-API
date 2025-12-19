namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors.Vendors;
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
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
		public Uri WebsiteUrl => new("https://www.southnorfolkandbroadland.gov.uk/rubbish-recycling/bin-collections-and-app/find-bin-collection-day");

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
				Keys = new List<string>() { "Rubbish" }.AsReadOnly(),
			},
			new()
			{
				Name = "Recycling",
				Colour = BinColour.Grey,
				Keys = new List<string>() { "Recycling" }.AsReadOnly(),
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Brown,
				Keys = new List<string>() { "Garden" }.AsReadOnly(),
			},
			new()
			{
				Name = "Food Waste",
				Colour = BinColour.Black,
				Keys = new List<string>() { "Food" }.AsReadOnly(),
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
		/// Regex for parsing bin names and dates from the dashboard HTML.
		/// </summary>
		[GeneratedRegex(@"<strong>(?<name>[^<]+)</strong><br\s*/?>\s*(?<date>[A-Za-z]+\s+\d{1,2}\s+[A-Za-z]+\s+\d{4})<br", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
		private static partial Regex BinDayRegex();

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
			// Prepare client-side request for getting dashboard with address context
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
			// Process dashboard response to parse bin days
			else if (clientSideResponse.RequestId == 1)
			{
				var binDays = new List<BinDay>();

				foreach (Match match in BinDayRegex().Matches(clientSideResponse.Content))
				{
					var binName = WebUtility.HtmlDecode(match.Groups["name"].Value).Trim();
					var dateString = match.Groups["date"].Value.Trim();

					// Parse date stirng (e.g. 'Friday 19 December 2025')
					var date = DateOnly.ParseExact(
						dateString,
						"dddd d MMMM yyyy",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);

					var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, binName);

					var binDay = new BinDay
					{
						Date = date,
						Address = address,
						Bins = matchedBins,
					};

					binDays.Add(binDay);
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
