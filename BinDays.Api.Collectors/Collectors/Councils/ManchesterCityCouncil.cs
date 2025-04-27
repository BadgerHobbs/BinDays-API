// This file was converted from the legacy dart implementation using AI.
// TODO: Manually review and improve this file.

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

	/// <summary>
	/// Collector implementation for Manchester City Council.
	/// </summary>
	internal sealed partial class ManchesterCityCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Manchester City Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.manchester.gov.uk/bincollections");

		/// <inheritdoc/>
		public override string GovUkId => "manchester";

		/// <summary>
		/// Base URL for the Verint API.
		/// </summary>
		private const string ApiBaseUrl = "https://manchester.form.uk.empro.verintcloudservices.com/api";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Paper & Card",
				Colour = "Blue",
				Keys = new List<string>() { "ahtm_dates_blue_pulpable_bin" }.AsReadOnly(),
			},
			new()
			{
				Name = "Metal, Glass & Plastic Bottles",
				Colour = "Brown",
				Keys = new List<string>() { "ahtm_dates_brown_commingled_bin" }.AsReadOnly(),
			},
			new()
			{
				Name = "Food & Garden Waste",
				Colour = "Green",
				Keys = new List<string>() { "ahtm_dates_green_organic_bin" }.AsReadOnly(),
			},
			new()
			{
				Name = "General Waste",
				Colour = "Black",
				Keys = new List<string>() { "ahtm_dates_black_bin" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting authorization token
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = $"{ApiBaseUrl}/citizen?archived=Y&preview=false&locale=en",
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
				// Get authorization token from response header
				var authToken = GetAuthorizationToken(clientSideResponse);

				// Prepare client-side request body as JSON
				var requestBody = new JsonObject
				{
					["name"] = "sr_bin_coll_day_checker",
					["data"] = new JsonObject
					{
						["addressnumber"] = "",
						["streetname"] = "",
						["postcode"] = postcode,
					},
					["email"] = "",
					["caseid"] = "",
					["xref"] = "",
					["xref1"] = "",
					["xref2"] = ""
				};

				var requestHeaders = new Dictionary<string, string>() {
					{"Authorization", authToken},
					{"content-type", "application/json"},
					{"Origin", "https://manchester.portal.uk.empro.verintcloudservices.com"}, // Added Origin as per Dart example
                    {"Referer", "https://manchester.portal.uk.empro.verintcloudservices.com/"} // Added Referer as per Dart example
                };

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = $"{ApiBaseUrl}/custom?action=widget-property-search&actionedby=location_search_property&loadform=true&access=citizen&locale=en",
					Method = "POST",
					Headers = requestHeaders,
					Body = requestBody.ToJsonString(),
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
				var responseJson = JsonNode.Parse(clientSideResponse.Content)!.AsObject();
				var addressesJson = responseJson["data"]!["prop_search_results"]!.AsArray();

				// Iterate through each address json, and create a new address object
				var addresses = new List<Address>();
				foreach (var addressNode in addressesJson)
				{
					if (addressNode == null) continue;

					string uid = addressNode["value"]!.GetValue<string>();
					string label = addressNode["label"]!.GetValue<string>(); // e.g., "1 Example Street, Town, Postcode"

					// Basic parsing of label - assumes format like "Property Street, Town, Postcode" or "Property Street, Postcode"
					var parts = label.Split(',');
					string propertyAndStreet = parts[0].Trim();
					string town = parts.Length > 2 ? parts[1].Trim() : string.Empty; // Take middle part as town if available

					// Attempt to split property number/name from street (simple split based on first space after a potential number)
					string property = propertyAndStreet;
					string street = string.Empty;
					int firstSpaceIndex = propertyAndStreet.IndexOf(' ');
					if (firstSpaceIndex > 0)
					{
						// Check if the first part is numeric (or contains a number) to decide split point
						bool firstPartIsNumeric = propertyAndStreet.Take(firstSpaceIndex).Any(char.IsDigit);
						if (firstPartIsNumeric)
						{
							property = propertyAndStreet.Substring(0, firstSpaceIndex);
							street = propertyAndStreet.Substring(firstSpaceIndex + 1);
						}
						// Else, keep the whole first part as property if it looks like a name (e.g., "Flat 1", "The Cottage")
						// This logic is simplified compared to the Dart version's iterative approach but covers common cases.
					}


					var address = new Address()
					{
						Property = property.Trim(),
						Street = street.Trim(),
						Town = town,
						Postcode = postcode,
						Uid = uid, // This is the object_id used later
					};

					addresses.Add(address);
				}

				// Sort addresses (basic sort by string representation for consistency with Dart)
				// A more robust sort would parse numbers properly.
				addresses = addresses.OrderBy(a => a.ToString()).ToList();


				var getAddressesResponse = new GetAddressesResponse()
				{
					Addresses = addresses.AsReadOnly(),
					NextClientSideRequest = null
				};

				return getAddressesResponse;
			}

			// Throw exception for invalid request
			throw new InvalidOperationException($"Invalid client-side request for GetAddresses. RequestId: {clientSideResponse?.RequestId}");
		}

		/// <inheritdoc/>
		public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting authorization token
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = $"{ApiBaseUrl}/citizen?archived=Y&preview=false&locale=en",
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
			// Prepare client-side request for getting property details (UPRN)
			else if (clientSideResponse?.RequestId == 1)
			{
				// Get authorization token from response header
				var authToken = GetAuthorizationToken(clientSideResponse);

				// Prepare client-side request body as JSON
				var requestBody = new JsonObject
				{
					["name"] = "sr_bin_coll_day_checker",
					["data"] = new JsonObject
					{
						["object_id"] = address.Uid // Uid from GetAddresses is the object_id
					},
					["email"] = "",
					["caseid"] = "",
					["xref"] = "",
					["xref1"] = "",
					["xref2"] = ""
				};

				var requestHeaders = new Dictionary<string, string>() {
					{"Authorization", authToken},
					{"content-type", "application/json"},
					{"Origin", "https://manchester.portal.uk.empro.verintcloudservices.com"},
					{"Referer", "https://manchester.portal.uk.empro.verintcloudservices.com/"}
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = $"{ApiBaseUrl}/custom?action=retrieve-property&actionedby=_KDF_optionSelected&loadform=true&access=citizen&locale=en",
					Method = "POST",
					Headers = requestHeaders,
					Body = requestBody.ToJsonString(),
				};

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = null,
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Prepare client-side request for getting bin collection dates
			else if (clientSideResponse?.RequestId == 2)
			{
				// Get authorization token from response header (it should be passed back by the client)
				var authToken = GetAuthorizationToken(clientSideResponse);

				// Parse response to get UPRN
				var responseJson = JsonNode.Parse(clientSideResponse.Content)!.AsObject();
				var uprn = responseJson["data"]!["UPRN"]!.GetValue<string>();

				// Calculate date range
				var now = DateTime.UtcNow; // Use UtcNow for consistency if needed, or DateTime.Now for local time
				var threeMonthsAhead = now.AddDays(90);
				var formattedNow = now.ToString("yyyy-MM-dd");
				var formattedThreeMonthsAhead = threeMonthsAhead.ToString("yyyy-MM-dd");

				// Prepare client-side request body as JSON
				var requestBody = new JsonObject
				{
					["name"] = "sr_bin_coll_day_checker",
					["data"] = new JsonObject
					{
						["uprn"] = uprn,
						["nextCollectionFromDate"] = formattedNow,
						["nextCollectionToDate"] = formattedThreeMonthsAhead
					},
					["email"] = "",
					["caseid"] = "",
					["xref"] = "",
					["xref1"] = "",
					["xref2"] = ""
				};

				var requestHeaders = new Dictionary<string, string>() {
					{"Authorization", authToken},
					{"content-type", "application/json"},
					{"Origin", "https://manchester.portal.uk.empro.verintcloudservices.com"},
					{"Referer", "https://manchester.portal.uk.empro.verintcloudservices.com/"}
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 3,
					Url = $"{ApiBaseUrl}/custom?action=bin_checker-get_bin_col_info&actionedby=_KDF_custom&loadform=true&access=citizen&locale=en",
					Method = "POST",
					Headers = requestHeaders,
					Body = requestBody.ToJsonString(),
				};

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = null,
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Process bin days from response
			else if (clientSideResponse?.RequestId == 3)
			{
				var aggregatedBinDays = new Dictionary<DateOnly, List<Bin>>();

				// Parse response content as JSON object
				var responseJson = JsonNode.Parse(clientSideResponse.Content)!.AsObject();
				var binData = responseJson["data"]!.AsObject();

				// Iterate through defined bin types
				foreach (var binType in this.binTypes)
				{
					var binTypeKey = binType.Keys.First(); // Assuming one key per bin type based on legacy code

					// Check if the key exists in the response data
					if (binData.ContainsKey(binTypeKey) && binData[binTypeKey] != null)
					{
						var dateString = binData[binTypeKey]!.GetValue<string>();

						// Split the date string (e.g., "15/04/2025 00:00:00;\n13/05/2025 00:00:00")
						var rawDates = dateString.Split(new[] { ";\n", ";" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

						foreach (var rawDate in rawDates)
						{
							// Parse the date string (e.g., "15/04/2025 00:00:00")
							// Format seems consistent as dd/MM/yyyy HH:mm:ss
							if (DateTime.TryParseExact(rawDate, "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var collectionDateTime))
							{
								var collectionDate = DateOnly.FromDateTime(collectionDateTime);

								// Aggregate bins by date
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
							// Optional: Log or handle parsing errors if needed
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
			throw new InvalidOperationException($"Invalid client-side request for GetBinDays. RequestId: {clientSideResponse?.RequestId}");
		}

		/// <summary>
		/// Retrieves the Authorization token from the client-side response headers.
		/// </summary>
		/// <param name="clientSideResponse">The client-side response.</param>
		/// <returns>The Authorization token.</returns>
		/// <exception cref="InvalidOperationException">Thrown if the Authorization header is missing.</exception>
		private static string GetAuthorizationToken(ClientSideResponse clientSideResponse)
		{
			if (clientSideResponse.Headers.TryGetValue("authorization", out var token))
			{
				return token;
			}

			// Check lower-case version as well, header names might be inconsistent casing
			if (clientSideResponse.Headers.TryGetValue("Authorization", out var tokenCaps))
			{
				return tokenCaps;
			}

			throw new InvalidOperationException("Authorization token not found in response headers.");
		}
	}
}
