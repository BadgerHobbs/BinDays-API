namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors.Vendors;
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text.Json;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Collector implementation for Plymouth Council.
	/// </summary>
	internal sealed partial class PlymouthCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Plymouth Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.plymouth.gov.uk/checkyourcollectionday");

		/// <inheritdoc/>
		public override string GovUkId => "plymouth";

		/// <summary>
		/// Regex to extract the session ID (sid) from HTML content.
		/// </summary>
		[GeneratedRegex(@"sid=([a-f0-9]+)")]
		private static partial Regex SessionIdRegex();

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly IReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Domestic",
				Colour = BinColour.Brown,
				Keys = new List<string>() { "DO" }.AsReadOnly(),
			},
			new()
			{
				Name = "Recycling",
				Colour = BinColour.Green,
				Keys = new List<string>() { "RE" }.AsReadOnly(),
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Black,
				Keys = new List<string>() { "GA", "OR" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Step 1: Get Session ID
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = "https://plymouth-self.achieveservice.com/en/AchieveForms/?form_uri=sandbox-publish://AF-Process-31283f9a-3ae7-4225-af71-bf3884e0ac1b/AF-Stagedba4a7d5-e916-46b6-abdb-643d38bec875/definition.json&redirectlink=/en&cancelRedirectLink=/en&consentMessage=yes",
					Method = "GET",
				};

				var getAddressesResponse = new GetAddressesResponse()
				{
					NextClientSideRequest = clientSideRequest
				};

				return getAddressesResponse;
			}
			// Step 2: Get Addresses using Session ID and Postcode
			else if (clientSideResponse.RequestId == 1)
			{
				// Get set-cookies from response
				var setCookies = clientSideResponse.Headers["set-cookie"];
				var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookies);

				// Extract Session ID from Step 1 response content
				var sessionId = SessionIdRegex().Match(clientSideResponse.Content).Groups[1].Value;

				// Prepare request body as a JSON string
				var requestBodyObject = new
				{
					formValues = new
					{
						Section1 = new
						{
							postcode_search = new
							{
								name = "postcode_search",
								type = "text",
								id = "AF-Field-c627b676-e7a7-428c-9196-2e59b2a36100",
								value_changed = true,
								section_id = "AF-Section-f62c31c7-a20e-4cb7-bec2-ed2260daa14c",
								label = "Postcode / Street Search (min 5 characters)",
								value = postcode,
								path = "root/addressDetails/postcode_search",
								valid = true,
							}
						}
					}
				};
				var requestBody = JsonSerializer.Serialize(requestBodyObject);

				var requestUrl = $"https://plymouth-self.achieveservice.com/apibroker/?api=RunLookup&id=560d5266e930f&sid={sessionId}";

				var requestHeaders = new Dictionary<string, string>() {
					{"content-type", "application/json; charset=UTF-8"},
					{"cookie", requestCookies},
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = requestUrl,
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
			// Step 3: Process Addresses from Response
			else if (clientSideResponse.RequestId == 2)
			{
				// Parse response content as JSON object
				var responseJson = JsonDocument.Parse(clientSideResponse.Content).RootElement;
				var rawAddresses = responseJson.GetProperty("integration").GetProperty("transformed").GetProperty("rows_data");

				// Iterate through each address object
				var addresses = new List<Address>();
				foreach (var property in rawAddresses.EnumerateObject())
				{
					var addressData = property.Value;

					string flat = addressData.GetProperty("flat").ToString();
					string house = addressData.GetProperty("house").ToString();
					string street = addressData.GetProperty("street").ToString();
					string town = addressData.GetProperty("town").ToString();
					string uprn = addressData.GetProperty("uprn").ToString();

					// Combine flat and house for property, ensuring no double spaces
					string addressProperty = $"{flat} {house}".Trim().Replace("  ", " ");

					var address = new Address()
					{
						Property = addressProperty,
						Street = street.Trim(),
						Town = town.Trim(),
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
			// Step 1: Get Session ID (same as GetAddresses Step 1)
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = "https://plymouth-self.achieveservice.com/en/AchieveForms/?form_uri=sandbox-publish://AF-Process-31283f9a-3ae7-4225-af71-bf3884e0ac1b/AF-Stagedba4a7d5-e916-46b6-abdb-643d38bec875/definition.json&redirectlink=/en&cancelRedirectLink=/en&consentMessage=yes",
					Method = "GET",
				};

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Step 2: Get Bin Days using Session ID and UPRN
			else if (clientSideResponse.RequestId == 1)
			{
				// Get set-cookies from response
				var setCookies = clientSideResponse.Headers["set-cookie"];
				var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookies);

				// Extract Session ID from Step 1 response content
				var sessionId = SessionIdRegex().Match(clientSideResponse.Content).Groups[1].Value;

				// Prepare request body as a JSON string
				var requestBodyObject = new
				{
					formValues = new
					{
						Section1 = new
						{
							number1 = new
							{
								name = "number1",
								type = "number",
								id = "AF-Field-f72d45bf-709a-477e-8f2f-0f974987af9c",
								value_changed = true,
								section_id = "AF-Section-e3363624-b9e6-4086-8bf2-ff38d6cd36e2",
								label = "UPRN",
								value = address.Uid,
								path = "root/number1",
							},
							lastncoll = new
							{
								name = "lastncoll",
								type = "text",
								id = "AF-Field-a752c466-dd33-4665-9e51-784382c7047e",
								value_changed = true,
								section_id = "AF-Section-e3363624-b9e6-4086-8bf2-ff38d6cd36e2",
								label = "lastncoll",
								value_label = "",
								value = "0",
								path = "root/lastncoll",
							},
							nextncoll = new
							{
								name = "nextncoll",
								type = "text",
								id = "AF-Field-34b90039-2a9f-42b0-a397-350774ca0edd",
								value_changed = true,
								section_id = "AF-Section-e3363624-b9e6-4086-8bf2-ff38d6cd36e2",
								label = "nextncoll",
								value = "8",
								path = "root/nextncoll",
							}
						}
					}
				};
				var requestBody = JsonSerializer.Serialize(requestBodyObject);

				var requestUrl = $"https://plymouth-self.achieveservice.com/apibroker/?api=RunLookup&id=5c99439d85f83&sid={sessionId}";

				var requestHeaders = new Dictionary<string, string>() {
					{"content-type", "application/json; charset=UTF-8"},
					{"cookie", requestCookies},
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = requestUrl,
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
			// Step 3: Process Bin Days from Response
			else if (clientSideResponse.RequestId == 2)
			{
				// Parse response content as JSON object
				var responseJson = JsonDocument.Parse(clientSideResponse.Content).RootElement;
				var rawBinDays = responseJson.GetProperty("integration").GetProperty("transformed").GetProperty("rows_data");

				// Iterate through each collection entry
				var binDays = new List<BinDay>();
				foreach (var property in rawBinDays.EnumerateObject())
				{
					var binDayData = property.Value;

					string? dateString = binDayData.GetProperty("Date").ToString();
					string? roundType = binDayData.GetProperty("Round_Type").ToString();

					// Parse date (e.g. '2025-05-07T00:00:00')
					var date = DateOnly.ParseExact(
						dateString,
						"yyyy-MM-ddTHH:mm:ss",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);

					// Find matching bin types based on the round type in their keys
					var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, roundType);

					var binDay = new BinDay()
					{
						Date = date,
						Address = address,
						Bins = matchedBins.AsReadOnly()
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
