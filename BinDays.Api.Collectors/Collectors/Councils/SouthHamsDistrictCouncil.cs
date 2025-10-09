namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Globalization;
	using System.Linq;
	using System.Text.Json;
	using System.Text.Json.Nodes;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Collector implementation for South Hams District Council.
	/// </summary>
	internal sealed partial class SouthHamsDistrictCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "South Hams District Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://waste.southhams.gov.uk/");

		/// <inheritdoc/>
		public override string GovUkId => "south-hams";

		/// <summary>
		/// Regex for the fcc_session_token value from an input field.
		/// </summary>
		[GeneratedRegex(@"<input[^>]*name=[""']fcc_session_token[""'][^>]*value=[""'](.*?)[""']")]
		private static partial Regex SessionTokenRegex();

		/// <summary>
		/// Regex for the title within <h3> tags.
		/// </summary>
		[GeneratedRegex(@"<h3.*?>\s*(.*?)\s*</h3>")]
		private static partial Regex ServiceRegex();

		/// <summary>
		/// Regex for the date following specific text and within <b> tags.
		/// </summary>
		[GeneratedRegex(@"Your next scheduled collection is\s*<b>\s*(.*?)\s*</b>")]
		private static partial Regex DateRegex();

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Recycling",
				Colour = BinColor.Green,
				Keys = new List<string>() { "Recycling" }.AsReadOnly(),
			},
			new()
			{
				Name = "Refuse",
				Colour = BinColor.Grey,
				Keys = new List<string>() { "Refuse" }.AsReadOnly(),
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColor.Brown,
				Keys = new List<string>() { "Garden" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting session id
			if (clientSideResponse == null)
			{
				// Prepare client-side request
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = "https://waste.southhams.gov.uk/",
					Method = "GET",
				};

				var getAddressesResponse = new GetAddressesResponse()
				{
					Addresses = null,
					NextClientSideRequest = clientSideRequest
				};

				return getAddressesResponse;
			}
			// Prepare client-side request for getting addresses
			else if (clientSideResponse?.RequestId == 1)
			{
				// Get set-cookies from response
				var setCookies = clientSideResponse.Headers["set-cookie"];
				var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookies);

				// Get session id from response content
				var sessionId = SessionTokenRegex().Match(clientSideResponse.Content).Groups[1].Value;

				// Prepare client-side request
				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
				{
					{"fcc_session_token", sessionId},
					{"postcode", postcode},
				});

				var requestHeaders = new Dictionary<string, string>() {
					{"x-requested-with", "XMLHttpRequest"},
					{"content-type", "application/x-www-form-urlencoded; charset=UTF-8"},
					{"cookie", requestCookies},
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = "https://waste.southhams.gov.uk/ajaxprocessor/getaddresses",
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
			else if (clientSideResponse?.RequestId == 2)
			{
				// Parse response content as JSON object
				var responseJson = JsonSerializer.Deserialize<JsonObject>(clientSideResponse.Content)!;
				var addressesJson = responseJson["addresses"]!.AsObject();

				// Iterate through each address json, and create a new address object
				var addresses = new List<Address>();
				foreach (var property in addressesJson)
				{
					JsonArray addressArray = property.Value!.AsArray();

					string uid = addressArray[0]!.GetValue<string>();
					string fullAddress = addressArray[1]!.GetValue<string>();

					var address = new Address()
					{
						Property = fullAddress,
						Street = string.Empty,
						Town = string.Empty,
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
			// Prepare client-side request for getting session id
			if (clientSideResponse == null)
			{
				// Prepare client-side request
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = "https://waste.southhams.gov.uk/",
					Method = "GET",
				};

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = null,
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Prepare client-side request for getting bin days
			else if (clientSideResponse?.RequestId == 1)
			{
				// Get set-cookies from response
				var setCookies = clientSideResponse.Headers["set-cookie"];
				var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookies);

				// Get session id from response content
				var sessionId = SessionTokenRegex().Match(clientSideResponse.Content).Groups[1].Value;

				// Prepare client-side request
				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
				{
					{"fcc_session_token", sessionId},
					{"uprn", address.Uid!},
				});

				var requestHeaders = new Dictionary<string, string>() {
					{"x-requested-with", "XMLHttpRequest"},
					{"content-type", "application/x-www-form-urlencoded; charset=UTF-8"},
					{"cookie", requestCookies},
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = "https://waste.southhams.gov.uk/mycollections/getcollectiondetails",
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
			else if (clientSideResponse?.RequestId == 2)
			{
				// Parse response content as JSON object
				var responseJson = JsonSerializer.Deserialize<JsonObject>(clientSideResponse.Content)!;
				var binDaysJson = responseJson["binCollections"]!["tile"]!.AsArray();

				// Iterate through each bin days html, and create a new bin days object
				var binDays = new List<BinDay>();
				foreach (var binDayHtml in binDaysJson)
				{
					// Using regex get the service and date
					var serviceMatch = ServiceRegex().Match(binDayHtml![0]!.ToString());
					var dateMatch = DateRegex().Match(binDayHtml![0]!.ToString());

					var service = serviceMatch.Groups[1].Value;
					var collectionDateString = dateMatch.Groups[1].Value;

					// Get matching bin types from the service using the keys
					var matchedBinTypes = _binTypes.Where(x => x.Keys.Any(y => service.Contains(y)));

					// Parse the date (e.g. 'tomorrow, Wednesday, 07 May 2025') to date only
					var date = DateOnly.ParseExact(
						collectionDateString.Split(",").Last().Trim(),
						"dd MMMM yyyy",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);


					var binDay = new BinDay()
					{
						Date = date,
						Address = address,
						Bins = matchedBinTypes.ToList().AsReadOnly()
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