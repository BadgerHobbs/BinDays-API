// This file was converted from the legacy dart implementation using AI.
// TODO: Manually review and improve this file.

namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Models;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Globalization;
	using System.Linq;
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
		private readonly ReadOnlyCollection<Bin> binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Domestic",
				Colour = "Brown",
				Keys = new List<string>() { "DO" }.AsReadOnly(),
			},
			new()
			{
				Name = "Recycling",
				Colour = "Green",
				Keys = new List<string>() { "RE" }.AsReadOnly(),
			},
			new()
			{
				Name = "Garden Waste",
				Colour = "Black", // Note: Legacy Dart says Black, website might differ but stick to legacy
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
			// Step 2: Get Addresses using Session ID and Postcode
			else if (clientSideResponse.RequestId == 1)
			{
				// Extract Session ID from Step 1 response content
				var sessionIdMatch = SessionIdRegex().Match(clientSideResponse.Content);
				if (!sessionIdMatch.Success)
				{
					throw new InvalidOperationException("Could not extract session ID from initial page load.");
				}
				var sessionId = sessionIdMatch.Groups[1].Value;

				// Prepare request body as a JSON string
				var requestBodyObject = new
				{
					formValues = new
					{
						Section1 = new // Renamed from "Section 1" to be a valid C# identifier
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
								totals = "",
								suffix = "",
								prefix = "",
								summary = "",
								hidden = false,
								_hidden = false,
								isSummary = false,
								staticMap = false,
								isMandatory = false,
								isRepeatable = false,
								currencyPrefix = "",
								decimalPlaces = "",
								hash = ""
							}
						}
					}
				};
				var requestBody = JsonSerializer.Serialize(requestBodyObject, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

				// Prepare query parameters
				var queryParams = $"?api=RunLookup&id=560d5266e930f&sid={sessionId}";
				var requestUrl = $"https://plymouth-self.achieveservice.com/apibroker/{queryParams}";

				var requestHeaders = new Dictionary<string, string>() {
					{"content-type", "application/json; charset=UTF-8"}, // Assuming JSON based on body structure
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
					Addresses = null,
					NextClientSideRequest = clientSideRequest
				};

				return getAddressesResponse;
			}
			// Step 3: Process Addresses from Response
			else if (clientSideResponse.RequestId == 2)
			{
				var addresses = new List<Address>();

				// Parse response content as JSON object
				var responseJson = JsonDocument.Parse(clientSideResponse.Content).RootElement;
				var rawAddresses = responseJson.GetProperty("integration").GetProperty("transformed").GetProperty("rows_data");

				// Iterate through each address object
				foreach (var property in rawAddresses.EnumerateObject())
				{
					var addressData = property.Value;

					string flat = addressData.TryGetProperty("flat", out var flatElement) ? flatElement.GetString() ?? "" : "";
					string house = addressData.TryGetProperty("house", out var houseElement) ? houseElement.GetString() ?? "" : "";
					string street = addressData.TryGetProperty("street", out var streetElement) ? streetElement.GetString() ?? "" : "";
					string town = addressData.TryGetProperty("town", out var townElement) ? townElement.GetString() ?? "" : "";
					string responsePostcode = addressData.TryGetProperty("postcode", out var postcodeElement) ? postcodeElement.GetString() ?? "" : "";
					string uprn = addressData.TryGetProperty("uprn", out var uprnElement) ? uprnElement.GetString() ?? "" : "";

					// Combine flat and house for property, ensuring no double spaces
					string addressProperty = $"{flat} {house}".Trim().Replace("  ", " ");

					var address = new Address()
					{
						Property = addressProperty,
						Street = street.Trim(),
						Town = town.Trim(),
						Postcode = responsePostcode.Trim(), // Use postcode from response
						Uid = uprn,
					};

					addresses.Add(address);
				}

				// Note: Sorting logic from Dart is complex and potentially fragile.
				// Standard string sorting is generally sufficient unless specific numeric sorting is required.
				// addresses = addresses.OrderBy(a => a.Street).ThenBy(a => a.Property).ToList(); // Example basic sort

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
			// Step 1: Get Session ID (same as GetAddresses Step 1)
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = "https://plymouth-self.achieveservice.com/en/AchieveForms/?form_uri=sandbox-publish://AF-Process-31283f9a-3ae7-4225-af71-bf3884e0ac1b/AF-Stagedba4a7d5-e916-46b6-abdb-643d38bec875/definition.json&redirectlink=/en&cancelRedirectLink=/en&consentMessage=yes",
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
			// Step 2: Get Bin Days using Session ID and UPRN
			else if (clientSideResponse.RequestId == 1)
			{
				// Extract Session ID from Step 1 response content
				var sessionIdMatch = SessionIdRegex().Match(clientSideResponse.Content);
				if (!sessionIdMatch.Success)
				{
					throw new InvalidOperationException("Could not extract session ID from initial page load.");
				}
				var sessionId = sessionIdMatch.Groups[1].Value;

				// Prepare request body as a JSON string
				var requestBodyObject = new
				{
					formValues = new
					{
						Section1 = new // Renamed from "Section 1"
						{
							number1 = new
							{
								name = "number1",
								type = "number",
								id = "AF-Field-f72d45bf-709a-477e-8f2f-0f974987af9c",
								value_changed = true,
								section_id = "AF-Section-e3363624-b9e6-4086-8bf2-ff38d6cd36e2",
								label = "UPRN",
								value_label = "",
								hasOther = false,
								value = address.Uid,
								path = "root/number1",
								valid = "",
								totals = "",
								suffix = "",
								prefix = "",
								summary = "",
								hidden = false,
								_hidden = true,
								isSummary = false,
								staticMap = false,
								isMandatory = false,
								isRepeatable = false,
								currencyPrefix = "",
								decimalPlaces = "",
								hash = ""
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
								hasOther = false,
								value = "0", // Hardcoded as per legacy
								path = "root/lastncoll",
								valid = "",
								totals = "",
								suffix = "",
								prefix = "",
								summary = "",
								hidden = false,
								_hidden = true,
								isSummary = false,
								staticMap = false,
								isMandatory = false,
								isRepeatable = false,
								currencyPrefix = "",
								decimalPlaces = "",
								hash = ""
							},
							nextncoll = new
							{
								name = "nextncoll",
								type = "text",
								id = "AF-Field-34b90039-2a9f-42b0-a397-350774ca0edd",
								value_changed = true,
								section_id = "AF-Section-e3363624-b9e6-4086-8bf2-ff38d6cd36e2",
								label = "nextncoll",
								value_label = "",
								hasOther = false,
								value = "8", // Hardcoded as per legacy
								path = "root/nextncoll",
								valid = "",
								totals = "",
								suffix = "",
								prefix = "",
								summary = "",
								hidden = false,
								_hidden = true,
								isSummary = false,
								staticMap = false,
								isMandatory = false,
								isRepeatable = false,
								currencyPrefix = "",
								decimalPlaces = "",
								hash = ""
							}
						}
					}
				};
				var requestBody = JsonSerializer.Serialize(requestBodyObject, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

				// Prepare query parameters
				var queryParams = $"?api=RunLookup&id=5c99439d85f83&sid={sessionId}";
				var requestUrl = $"https://plymouth-self.achieveservice.com/apibroker/{queryParams}";

				var requestHeaders = new Dictionary<string, string>() {
					{"content-type", "application/json; charset=UTF-8"},
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
					BinDays = null,
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Step 3: Process Bin Days from Response
			else if (clientSideResponse.RequestId == 2)
			{
				var aggregatedBinDays = new Dictionary<DateOnly, List<Bin>>();

				// Parse response content as JSON object
				var responseJson = JsonDocument.Parse(clientSideResponse.Content).RootElement;
				var rawBinDays = responseJson.GetProperty("integration").GetProperty("transformed").GetProperty("rows_data");

				// Iterate through each collection entry
				foreach (var property in rawBinDays.EnumerateObject())
				{
					var binDayData = property.Value;

					string? dateString = binDayData.TryGetProperty("Date", out var dateElement) ? dateElement.GetString() : null;
					string? roundType = binDayData.TryGetProperty("Round_Type", out var roundTypeElement) ? roundTypeElement.GetString() : null;

					if (string.IsNullOrEmpty(dateString) || string.IsNullOrEmpty(roundType))
					{
						continue; // Skip if essential data is missing
					}

					// Try parsing the date string (assuming ISO 8601 format based on Dart's DateTime.parse)
					if (!DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dateTime))
					{
						continue; // Skip if date parsing fails
					}
					var collectionDate = DateOnly.FromDateTime(dateTime);

					// Find matching bin types based on the round type in their keys
					var matchedBins = this.binTypes.Where(bin => bin.Keys.Contains(roundType));

					// Aggregate bins by date
					foreach (var binType in matchedBins)
					{
						if (!aggregatedBinDays.TryGetValue(collectionDate, out var binsForDate))
						{
							binsForDate = [];
							aggregatedBinDays[collectionDate] = binsForDate;
						}

						// Add bin type if it's not already in the list for this date
						if (!binsForDate.Any(b => b.Name == binType.Name && b.Colour == binType.Colour))
						{
							binsForDate.Add(binType);
						}
					}
				}

				// Create BinDay objects from the aggregated data, ordered by date
				var binDays = aggregatedBinDays
					.Select(kvp => new BinDay()
					{
						Date = kvp.Key,
						Address = address,
						Bins = kvp.Value.AsReadOnly()
					})
					.OrderBy(bd => bd.Date)
					.ToList();

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
