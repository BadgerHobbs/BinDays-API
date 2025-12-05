namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors.Vendors;
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Collector implementation for Northumberland County Council.
	/// </summary>
	internal sealed partial class NorthumberlandCountyCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Northumberland County Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://bincollection.northumberland.gov.uk/postcode");

		/// <inheritdoc/>
		public override string GovUkId => "northumberland";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly IReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "General Waste",
				Colour = BinColour.Green,
				Keys = ["General waste"],
			},
			new ()
			{
				Name = "Garden Waste",
				Colour = BinColour.Brown,
				Keys = ["Garden waste"],
			},
			new()
			{
				Name = "Recycling",
				Colour = BinColour.Blue,
				Keys = ["Recycling"],
			},
		}.AsReadOnly();

		/// <summary>
		/// Regex for the csrf token values from input fields.
		/// </summary>
		[GeneratedRegex(@"<input[^>]*?name=""_csrf""[^>]*?value=""(?<tokenValue>[^""]*)""[^>]*?/?>")]
		private static partial Regex CsrfTokenRegex();

		/// <summary>
		/// Regex for the addresses from the elements.
		/// </summary>
		[GeneratedRegex(@"<option value=""(?<uprn>\d+)"">(?<address>.*?)<\/option>")]
		private static partial Regex AddressesRegex();

		/// <summary>
		/// Regex for the bin collections from the data table elements.
		/// </summary>
		[GeneratedRegex(@"<tr class=""govuk-table__row"">\s*<th scope=""row"" class=""govuk-table__header"">(?<Date>[\d]+(?:st|nd|rd|th)? [A-Za-z]+)<\/th>\s*<td class=""govuk-table__cell"">.*?<\/td>\s*<td class=""govuk-table__cell"">(?<BinType>.*?)<\/td>\s*<\/tr>")]
		private static partial Regex BinCollectionsRegex();

		/// <summary>
		/// Regex for removing ordinal indicators from dates.
		/// </summary>
		[GeneratedRegex(@"(st|nd|rd|th)")]
		private static partial Regex OrdinalIndicatorsRegex();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting token
			if (clientSideResponse == null)
			{
				// Prepare client-side request
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = "https://bincollection.northumberland.gov.uk/postcode",
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
				// Get csrf token from response
				var csrfToken = CsrfTokenRegex().Match(clientSideResponse.Content).Groups["tokenValue"].Value;

				// Get set-cookies from response
				var setCookies = clientSideResponse.Headers["set-cookie"];
				var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookies);

				// Prepare client-side request
				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
				{
					{"_csrf", csrfToken},
					{"postcode", postcode},
				});

				var requestHeaders = new Dictionary<string, string>() {
					{"user-agent", Constants.UserAgent},
					{"content-type", "application/x-www-form-urlencoded"},
					{"cookie", requestCookies},
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = "https://bincollection.northumberland.gov.uk/postcode",
					Method = "POST",
					Headers = requestHeaders,
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
				// Get addresses from response
				var rawAddresses = AddressesRegex().Matches(clientSideResponse.Content)!;

				// Iterate through each address, and create a new address object
				var addresses = new List<Address>();
				foreach (Match rawAddress in rawAddresses)
				{
					var property = rawAddress.Groups["address"].Value;
					var uprn = rawAddress.Groups["uprn"].Value;

					var address = new Address()
					{
						Property = property,
						Postcode = postcode,
						Uid = uprn,
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
			// Prepare client-side request for getting token
			if (clientSideResponse == null)
			{
				// Prepare client-side request
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = "https://bincollection.northumberland.gov.uk/postcode",
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
			else if (clientSideResponse.RequestId == 1)
			{
				// Get csrf token from response
				var csrfToken = CsrfTokenRegex().Match(clientSideResponse.Content).Groups["tokenValue"].Value;

				// Get set-cookies from response
				var setCookies = clientSideResponse.Headers["set-cookie"];
				var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookies);

				// Prepare client-side request
				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
				{
					{"_csrf", csrfToken},
					{"address", address.Uid!},
				});

				var requestHeaders = new Dictionary<string, string>() {
					{"user-agent", Constants.UserAgent},
					{"content-type", "application/x-www-form-urlencoded"},
					{"cookie", requestCookies},
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = "https://bincollection.northumberland.gov.uk/address-select",
					Method = "POST",
					Headers = requestHeaders,
					Body = requestBody,
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
				// Get bin collections from response
				var rawBinCollections = BinCollectionsRegex().Matches(clientSideResponse.Content)!;

				// Iterate through each bin collection, and create a new bin day object
				var binDays = new List<BinDay>();
				foreach (Match rawBinCollection in rawBinCollections)
				{
					var dateStr = rawBinCollection.Groups["Date"].Value;
					var binTypeStr = rawBinCollection.Groups["BinType"].Value;

					// Remove ordinal indicators (st, nd, rd, th) from the date string
					dateStr = OrdinalIndicatorsRegex().Replace(dateStr, "");

					// Parse the date
					var date = DateOnly.ParseExact(
						dateStr,
						"d MMMM",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);

					// If the parsed date is in a month that has already passed this year, assume it's for next year
					if (date.Month < DateTime.Now.Month)
					{
						date = date.AddYears(1);
					}

					// Get matching bin types from the type using the keys
					var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, binTypeStr);

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
