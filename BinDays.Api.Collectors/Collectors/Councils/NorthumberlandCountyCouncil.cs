namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
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
		public Uri WebsiteUrl => new("https://www.northumberland.gov.uk/Waste/Household-waste/Household-bin-collections/Bin-Calendars.aspx");

		/// <inheritdoc/>
		public override string GovUkId => "northumberland";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> binTypes = new List<Bin>()
		{
			new()
			{
				Name = "General Waste",
				Colour = "Green",
				Keys = new List<string>() { "800068" }.AsReadOnly(),
			},
			new()
			{
				Name = "Garden Waste",
				Colour = "Brown",
				Keys = new List<string>() { "FF7171" }.AsReadOnly(),
			},
			new()
			{
				Name = "Recycling",
				Colour = "Blue",
				Keys = new List<string>() { "FECD00" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <summary>
		/// Regex for the csrf token values from input fields.
		/// </summary>
		[GeneratedRegex(@"<input[^>]*?(?:name|id)=[""']__CMSCsrfToken[""'][^>]*?value=[""'](?<tokenValue>[^""']*)[""'][^>]*?/?>")]
		private static partial Regex CsrfTokenRegex();

		/// <summary>
		/// Regex for the viewstate token values from input fields.
		/// </summary>
		[GeneratedRegex(@"<input[^>]*?(?:name|id)=[""']__VIEWSTATE[""'][^>]*?value=[""'](?<viewStateValue>[^""']*)[""'][^>]*?/?>")]
		private static partial Regex ViewStateTokenRegex();

		/// <summary>
		/// Regex for the addresses from the elements.
		/// </summary>
		[GeneratedRegex(@"<a\s+id=""[^""]*AddPick(?<uprn>\d+)""[^>]*>(?<address>.*?)<\/a>")]
		private static partial Regex AddressesRegex();

		/// <summary>
		/// Regex for the bin collection months from the data table elements.
		/// </summary>
		[GeneratedRegex(@"(?<MonthHtml>\<div style=""float: left; padding-right: 10px;"">[\s\S]*?\<td align=""center"" style=""width:70%;"">(?<MonthYear>[A-Za-z]+ \d{4})\<\/td>[\s\S]*?\<\/div>)")]
		private static partial Regex BinCollectionMonthsRegex();

		/// <summary>
		/// Regex for the bin days from the collection string.
		/// </summary>
		[GeneratedRegex(@"\<td.*?style="".*?background-color:#(?<Color>[0-9A-Fa-f]{6});.*?"".*?>(?<Day>\d{1,2})\<\/td>")]
		private static partial Regex MonthBinCollectionsRegex();

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
					Url = "https://www.northumberland.gov.uk/Waste/Household-waste/Household-bin-collections/Bin-Calendars.aspx",
					Method = "GET",
					Headers = new Dictionary<string, string>() {
						{"user-agent", Constants.UserAgent},
					},
					Body = string.Empty,
				};

				var getAddressesResponse = new GetAddressesResponse()
				{
					Addresses = null,
					NextClientSideRequest = clientSideRequest
				};

				return getAddressesResponse;
			}
			// Prepare client-side request for getting addresses
			else if (clientSideResponse.RequestId == 1)
			{
				// Get csrf token and viewstate from response
				var csrfToken = CsrfTokenRegex().Match(clientSideResponse.Content).Groups[1].Value;
				var viewState = ViewStateTokenRegex().Match(clientSideResponse.Content).Groups[1].Value;

				// Get set-cookies from response
				var setCookies = clientSideResponse.Headers["set-cookie"];
				var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookies);

				// Prepare client-side request
				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
				{
					{"__CMSCsrfToken", csrfToken},
					{"__VIEWSTATE", viewState},
					{"p$lt$ctl04$pageplaceholder$p$lt$ctl02$WasteCollectionCalendars$NCCAddressLookup$txtPostcode", postcode},
					{"p$lt$ctl04$pageplaceholder$p$lt$ctl02$WasteCollectionCalendars$NCCAddressLookup$butLookup", "Lookup Address"},
				});

				var requestHeaders = new Dictionary<string, string>() {
					{"user-agent", Constants.UserAgent},
					{"content-type", "application/x-www-form-urlencoded"},
					{"cookie", requestCookies},
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = "https://www.northumberland.gov.uk/Waste/Household-waste/Household-bin-collections/Bin-Calendars.aspx",
					Method = "POST",
					Headers = requestHeaders,
					Body = requestBody,
				};

				var getAddressesResponse = new GetAddressesResponse()
				{
					Addresses = null,
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
						Street = string.Empty,
						Town = string.Empty,
						Postcode = postcode,
						Uid = uprn,
					};

					addresses.Add(address);
				}

				var getAddressesResponse = new GetAddressesResponse()
				{
					Addresses = addresses.AsReadOnly(),
					NextClientSideRequest = null
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
					Url = "https://www.northumberland.gov.uk/Waste/Household-waste/Household-bin-collections/Bin-Calendars.aspx",
					Method = "GET",
					Headers = new Dictionary<string, string>() {
						{"user-agent", Constants.UserAgent},
					},
					Body = string.Empty,
				};

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = null,
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Prepare client-side request for getting bin days
			else if (clientSideResponse.RequestId == 1)
			{
				// Get csrf token and viewstate from response
				var csrfToken = CsrfTokenRegex().Match(clientSideResponse.Content).Groups[1].Value;
				var viewState = ViewStateTokenRegex().Match(clientSideResponse.Content).Groups[1].Value;

				// Get set-cookies from response
				var setCookies = clientSideResponse.Headers["set-cookie"];
				var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookies);

				// Prepare client-side request
				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
				{
					{"__CMSCsrfToken", csrfToken},
					{"__VIEWSTATE", viewState},
					{"p$lt$ctl04$pageplaceholder$p$lt$ctl02$WasteCollectionCalendars$NCCAddressLookup$txtPostcode", address.Postcode!},
					{"p$lt$ctl04$pageplaceholder$p$lt$ctl02$WasteCollectionCalendars$ddCalendarSize", "12"},
					{"p$lt$ctl04$pageplaceholder$p$lt$ctl02$WasteCollectionCalendars$butCalRefresh", "Refresh"},
					{"p$lt$ctl04$pageplaceholder$p$lt$ctl02$WasteCollectionCalendars$hidU", address.Uid!},
				});

				var requestHeaders = new Dictionary<string, string>() {
					{"user-agent", Constants.UserAgent},
					{"content-type", "application/x-www-form-urlencoded"},
					{"cookie", requestCookies},
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = "https://www.northumberland.gov.uk/Waste/Household-waste/Household-bin-collections/Bin-Calendars.aspx",
					Method = "POST",
					Headers = requestHeaders,
					Body = requestBody,
				};

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = null,
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Process bin days from response
			else if (clientSideResponse.RequestId == 2)
			{
				// Get bin collection months from response
				var rawBinCollectionMonths = BinCollectionMonthsRegex().Matches(clientSideResponse.Content)!;

				// Iterate through each bin collection month, and create a new bin day object
				var binDays = new List<BinDay>();
				foreach (Match rawBinCollectionMonth in rawBinCollectionMonths)
				{
					var monthYear = rawBinCollectionMonth.Groups["MonthYear"].Value;
					var monthHtml = rawBinCollectionMonth.Groups["MonthHtml"].Value;

					// Get bin dates from the collection
					var rawMonthBinCollections = MonthBinCollectionsRegex().Matches(monthHtml);

					// Iterate through each bin date, and create a new bin day object
					foreach (Match rawMonthBinCollection in rawMonthBinCollections)
					{
						var day = rawMonthBinCollection.Groups["Day"].Value;
						var hexColor = rawMonthBinCollection.Groups["Color"].Value;

						// Get matching bin types from the type using the keys
						var matchedBinTypes = binTypes.Where(x => x.Keys.Any(y => hexColor.Contains(y)));

						var binDay = new BinDay()
						{
							Date = DateOnly.ParseExact(
								$"{monthYear} {day}",
								"MMMM yyyy d",
								CultureInfo.InvariantCulture,
								DateTimeStyles.None
							),
							Address = address,
							Bins = matchedBinTypes.ToList().AsReadOnly()
						};

						binDays.Add(binDay);
					}
				}

				// Filter out bin days in the past
				binDays = [.. ProcessingUtilities.GetFutureBinDays(binDays)];

				// Merge bin days that fall on the same date
				binDays = [.. ProcessingUtilities.MergeBinDays(binDays)];

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = binDays.AsReadOnly(),
					NextClientSideRequest = null
				};

				return getBinDaysResponse;
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}
	}
}
