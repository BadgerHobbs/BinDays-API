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
	/// Collector implementation for Wigan Metropolitan Borough Council.
	/// </summary>
	internal sealed partial class WiganMetropolitanBoroughCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Wigan Metropolitan Borough Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://apps.wigan.gov.uk/MyNeighbourhood/Search.aspx");

		/// <inheritdoc/>
		public override string GovUkId => "wigan";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Black Bin",
				Colour = "Black",
				Keys = new List<string>() { "BlackBin" }.AsReadOnly(),
			},
			new()
			{
				Name = "Brown Bin",
				Colour = "Brown",
				Keys = new List<string>() { "BrownBin" }.AsReadOnly(),
			},
			new()
			{
				Name = "Green Bin",
				Colour = "Green",
				Keys = new List<string>() { "GreenBin" }.AsReadOnly(),
			},
			new()
			{
				Name = "Blue Bin",
				Colour = "Blue",
				Keys = new List<string>() { "BlueBin" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <summary>
		/// Regex for the viewstate token values from input fields.
		/// </summary>
		[GeneratedRegex(@"<input[^>]*?(?:name|id)=[""']__VIEWSTATE[""'][^>]*?value=[""'](?<viewStateValue>[^""']*)[""'][^>]*?/?>")]
		private static partial Regex ViewStateTokenRegex();

		/// <summary>
		/// Regex for the event validation values from input fields.
		/// </summary>
		[GeneratedRegex(@"<input[^>]*?(?:name|id)=[""']__EVENTVALIDATION[""'][^>]*?value=[""'](?<viewStateValue>[^""']*)[""'][^>]*?/?>")]
		private static partial Regex EventValidationRegex();

		/// <summary>
		/// Regex for the addresses from the options elements.
		/// </summary>
		[GeneratedRegex(@"<option value=""(?<uid>UPRN\d+)"">(?<address>[^<]+)</option>")]
		private static partial Regex AddressesRegex();

		/// <summary>
		/// Regex for the bin days from the data elements.
		/// </summary>
		[GeneratedRegex(@"(?s)<div id=""ContentPlaceHolder1_(?<binType>\w+Bin)Container"".*?<h3>Next Collection</h3>.*?<span class='bin-date-number'>(?<day>\d{1,2})</span>.*?<span class='bin-date-month'>(?<month>\w{3})</span><span class='bin-date-year'>(?<year>\d{4})</span>")]
		private static partial Regex BinDaysRegex();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting token
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = "https://apps.wigan.gov.uk/MyNeighbourhood/Search.aspx",
					Method = "GET",
					Headers = new Dictionary<string, string>() {
						{"user-agent", Constants.UserAgent},
					},
					Body = string.Empty,
				};

				return new GetAddressesResponse { Addresses = null, NextClientSideRequest = clientSideRequest };
			}
			// Prepare client-side request for getting addresses
			else if (clientSideResponse.RequestId == 1)
			{
				var viewState = ViewStateTokenRegex().Match(clientSideResponse.Content).Groups["viewStateValue"].Value;
				var eventValidation = EventValidationRegex().Match(clientSideResponse.Content).Groups["viewStateValue"].Value;

				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
				{
					{"__VIEWSTATE", viewState},
					{"__VIEWSTATEGENERATOR", "F01E8114"},
					{"__EVENTVALIDATION", eventValidation},
					{"ctl00$ContentPlaceHolder1$txtPostcode", postcode},
					{"ctl00$ContentPlaceHolder1$btnPostcodeSearch", "Search"},
				});

				var requestHeaders = new Dictionary<string, string>() {
					{"user-agent", Constants.UserAgent},
					{"content-type", "application/x-www-form-urlencoded"},
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = "https://apps.wigan.gov.uk/MyNeighbourhood/Search.aspx",
					Method = "POST",
					Headers = requestHeaders,
					Body = requestBody,
				};

				return new GetAddressesResponse { Addresses = null, NextClientSideRequest = clientSideRequest };
			}
			// Process addresses from response
			else if (clientSideResponse.RequestId == 2)
			{
				var rawAddresses = AddressesRegex().Matches(clientSideResponse.Content)!;
				var addresses = new List<Address>();

				foreach (Match rawAddress in rawAddresses)
				{
					addresses.Add(new Address()
					{
						Property = rawAddress.Groups["address"].Value.Trim(),
						Street = string.Empty,
						Town = string.Empty,
						Postcode = postcode,
						Uid = rawAddress.Groups["uid"].Value,
					});
				}

				return new GetAddressesResponse { Addresses = addresses.AsReadOnly(), NextClientSideRequest = null };
			}

			throw new InvalidOperationException("Invalid client-side request.");
		}

		/// <inheritdoc/>
		public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting token
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = "https://apps.wigan.gov.uk/MyNeighbourhood/Search.aspx",
					Method = "GET",
					Headers = new Dictionary<string, string>() {
						{"user-agent", Constants.UserAgent},
					},
					Body = string.Empty,
				};

				return new GetBinDaysResponse { BinDays = null, NextClientSideRequest = clientSideRequest };
			}
			// Prepare client-side request to get the address selection page
			else if (clientSideResponse.RequestId == 1)
			{
				var viewState = ViewStateTokenRegex().Match(clientSideResponse.Content).Groups["viewStateValue"].Value;
				var eventValidation = EventValidationRegex().Match(clientSideResponse.Content).Groups["viewStateValue"].Value;

				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
				{
					{"__VIEWSTATE", viewState},
					{"__VIEWSTATEGENERATOR", "F01E8114"},
					{"__EVENTVALIDATION", eventValidation},
					{"ctl00$ContentPlaceHolder1$txtPostcode", address.Postcode!},
					{"ctl00$ContentPlaceHolder1$btnPostcodeSearch", "Search"},
				});

				var requestHeaders = new Dictionary<string, string>() {
					{"user-agent", Constants.UserAgent},
					{"content-type", "application/x-www-form-urlencoded"},
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = "https://apps.wigan.gov.uk/MyNeighbourhood/Search.aspx",
					Method = "POST",
					Headers = requestHeaders,
					Body = requestBody,
				};

				return new GetBinDaysResponse { BinDays = null, NextClientSideRequest = clientSideRequest };
			}
			// Prepare client-side request to get bin collection data
			else if (clientSideResponse.RequestId == 2)
			{
				var viewState = ViewStateTokenRegex().Match(clientSideResponse.Content).Groups["viewStateValue"].Value;
				var eventValidation = EventValidationRegex().Match(clientSideResponse.Content).Groups["viewStateValue"].Value;

				string requestCookies = clientSideResponse.Headers["set-cookie"];

				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
				{
					{"__EVENTTARGET", "ctl00$ContentPlaceHolder1$lstAddresses"},
					{"__VIEWSTATE", viewState},
					{"__VIEWSTATEGENERATOR", "F01E8114"},
					{"__EVENTVALIDATION", eventValidation},
					{"ctl00$ContentPlaceHolder1$txtPostcode", address.Postcode!},
					{"ctl00$ContentPlaceHolder1$lstAddresses", address.Uid!},
				});

				var requestHeaders = new Dictionary<string, string>() {
					{"user-agent", Constants.UserAgent},
					{"content-type", "application/x-www-form-urlencoded"},
					{"cookie", requestCookies},
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 3,
					Url = "https://apps.wigan.gov.uk/MyNeighbourhood/Search.aspx",
					Method = "POST",
					Headers = requestHeaders,
					Body = requestBody,
				};

				return new GetBinDaysResponse { BinDays = null, NextClientSideRequest = clientSideRequest };
			}
			// Process bin days from response
			else if (clientSideResponse.RequestId == 3)
			{
				// Get bin days from response
				var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content)!;
				var binDays = new List<BinDay>();

				// Iterate through each bin day, and create a new bin day object
				foreach (Match rawBinDay in rawBinDays)
				{
					var binTypeKey = rawBinDay.Groups["binType"].Value;
					var day = rawBinDay.Groups["day"].Value;
					var month = rawBinDay.Groups["month"].Value;
					var year = rawBinDay.Groups["year"].Value;

					// Parse the collection date
					var date = DateOnly.ParseExact(
						$"{day} {month} {year}",
						"d MMM yyyy",
						CultureInfo.InvariantCulture
					);

					// Get matching bin types from the type using the keys
					var matchedBinTypes = binTypes.Where(b => b.Keys.Contains(binTypeKey));

					binDays.Add(new BinDay()
					{
						Date = date,
						Address = address,
						Bins = matchedBinTypes.ToList().AsReadOnly()
					});
				}

				// Filter out bin days in the past
				binDays = [.. ProcessingUtilities.GetFutureBinDays(binDays)];

				// Merge bin days that fall on the same date
				binDays = [.. ProcessingUtilities.MergeBinDays(binDays)];

				return new GetBinDaysResponse { BinDays = binDays.AsReadOnly(), NextClientSideRequest = null };
			}

			throw new InvalidOperationException("Invalid client-side request.");
		}
	}
}