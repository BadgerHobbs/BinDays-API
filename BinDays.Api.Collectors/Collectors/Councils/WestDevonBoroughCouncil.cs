namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System.Collections.ObjectModel;
	using System.Globalization;
	using System.Text.Json;
	using System.Text.Json.Nodes;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Collector implementation for West Devon Borough Council.
	/// </summary>
	internal sealed partial class WestDevonBoroughCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "West Devon Borough Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://westdevon.fccenvironment.co.uk/mycollections");

		/// <inheritdoc/>
		public override string GovUkId => "west-devon";

		/// <summary>
		/// Regex for the title within <h3> tags
		/// </summary>
		[GeneratedRegex(@"<h3.*?>\s*(.*?)\s*</h3>")]
		private static partial Regex ServiceRegex();

		/// <summary>
		/// Regex for the date following specific text and within <b> tags
		/// </summary>
		[GeneratedRegex(@"Your next scheduled collection is\s*<b>\s*(.*?)\s*</b>")]
		private static partial Regex DateRegex();

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Plastic & Metal Recycling",
				Colour = "White",
				Keys = new List<string>() { "Recycling and Food" }.AsReadOnly(),
				Type = "Bag",
			},
			new()
			{
				Name = "Paper, Glass, & Cartons Recycling",
				Colour = "Green",
				Keys = new List<string>() { "Recycling and Food" }.AsReadOnly(),
				Type = "Box",
			},
			new()
			{
				Name = "Cardboard, Batteries, Ink, & Clothes Recycling",
				Colour = "Green",
				Keys = new List<string>() { "Recycling and Food" }.AsReadOnly(),
				Type = "Box",
			},
			new()
			{
				Name = "Food Waste",
				Colour = "Grey",
				Keys = new List<string>() { "Recycling and Food" }.AsReadOnly(),
				Type = "Bin",
			},
			new()
			{
				Name = "General Waste",
				Colour = "Brown",
				Keys = new List<string>() { "Refuse" }.AsReadOnly(),
			},
			new()
			{
				Name = "Garden Waste",
				Colour = "Green",
				Keys = new List<string>() { "Garden" }.AsReadOnly(),
				Type = "Sack",
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
					Url = "https://westdevon.fccenvironment.co.uk/mycollections",
					Method = "GET",
					Headers = [],
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
			else if (clientSideResponse?.RequestId == 1)
			{
				// Get session id from response header
				var sessionId = GetSessionId(clientSideResponse);

				// Prepare client-side request
				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
				{
					{"fcc_session_token", sessionId},
					{"postcode", postcode},
				});

				var requestHeaders = new Dictionary<string, string>() {
					{"cookie", $"fcc_session_cookie={sessionId}"},
					{"x-requested-with", "XMLHttpRequest"},
					{"content-type", "application/x-www-form-urlencoded; charset=UTF-8"},
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = "https://westdevon.fccenvironment.co.uk/ajaxprocessor/getaddresses",
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
			// Prepare client-side request for getting session id
			if (clientSideResponse == null)
			{
				// Prepare client-side request
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = "https://westdevon.fccenvironment.co.uk/mycollections",
					Method = "GET",
					Headers = [],
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
			else if (clientSideResponse?.RequestId == 1)
			{
				// Get session id from response header
				var sessionId = GetSessionId(clientSideResponse);

				// Prepare client-side request
				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
				{
					{"fcc_session_token", sessionId},
					{"uprn", address.Uid!},
				});

				var requestHeaders = new Dictionary<string, string>() {
					{"cookie", $"fcc_session_cookie={sessionId}"},
					{"x-requested-with", "XMLHttpRequest"},
					{"content-type", "application/x-www-form-urlencoded; charset=UTF-8"},
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = "https://westdevon.fccenvironment.co.uk/ajaxprocessor/getcollectiondetails",
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
					var service = ServiceRegex().Match(binDayHtml![0]!.ToString()).Groups[1].Value;
					var collectionDate = DateRegex().Match(binDayHtml![0]!.ToString()).Groups[1].Value;

					// Get matching bin types from the service using the keys
					var binTypes = this.binTypes.Where(x => x.Keys.Any(y => service.Contains(y)));

					// Parse the date (e.g. 'tomorrow, Tuesday, 15 April 2025') to date only
					var date = DateOnly.ParseExact(
						collectionDate.Split(",").Last().Trim(),
						"dd MMMM yyyy",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);

					var binDay = new BinDay()
					{
						Date = date,
						Address = address,
						Bins = binTypes.ToList().AsReadOnly()
					};

					binDays.Add(binDay);
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

		/// <summary>
		/// Retrieves the session ID from the client-side response.
		/// </summary>
		/// <param name="clientSideResponse">The client-side response.</param>
		/// <returns>The session ID.</returns>
		private static string GetSessionId(ClientSideResponse clientSideResponse)
		{
			// Get session id from header cookie
			// e.g. set-cookie: fcc_session_cookie=7e397cdc04a1195d60be4255d153ebee; ...
			var cookie = clientSideResponse.Headers["set-cookie"];

			var sessionId = cookie.Split(";")
				.Where(x => x.Contains("fcc_session_cookie"))
				.First()
				.Split("=")
				.Last();

			return sessionId;
		}
	}
}
