namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Globalization;
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
					Url = "https://manchester.form.uk.empro.verintcloudservices.com/api/citizen?archived=Y&preview=false&locale=en",
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
				var authToken = clientSideResponse.Headers["Authorization"];

				// Prepare client-side request body as JSON
				var requestBody = new JsonObject
				{
					["name"] = "sr_bin_coll_day_checker",
					["data"] = new JsonObject
					{
						["postcode"] = postcode,
					},
				};

				var requestHeaders = new Dictionary<string, string>() {
					{"Authorization", authToken},
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = "https://manchester.form.uk.empro.verintcloudservices.com/api/custom?action=widget-property-search&actionedby=location_search_property&loadform=true&access=citizen&locale=en",
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
					string uid = addressNode!["value"]!.GetValue<string>();
					string property = addressNode["label"]!.GetValue<string>();

					var address = new Address()
					{
						Property = property.Trim(),
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
			// Prepare client-side request for getting authorization token
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = "https://manchester.form.uk.empro.verintcloudservices.com/api/citizen?archived=Y&preview=false&locale=en",
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
				var authToken = clientSideResponse.Headers["Authorization"];

				// Prepare client-side request body as JSON
				var requestBody = new JsonObject
				{
					["name"] = "sr_bin_coll_day_checker",
					["data"] = new JsonObject
					{
						["object_id"] = address.Uid
					},
				};

				var requestHeaders = new Dictionary<string, string>() {
					{"Authorization", authToken},
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = "https://manchester.form.uk.empro.verintcloudservices.com/api/custom?action=retrieve-property&actionedby=_KDF_optionSelected&loadform=true&access=citizen&locale=en",
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
				// Get authorization token from response header
				var authToken = clientSideResponse.Headers["Authorization"];

				// Parse response to get UPRN
				var responseJson = JsonNode.Parse(clientSideResponse.Content)!.AsObject();
				var uprn = responseJson["data"]!["UPRN"]!.GetValue<string>();

				// Calculate date range
				var now = DateTime.UtcNow;
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
				};

				var requestHeaders = new Dictionary<string, string>() {
					{"Authorization", authToken},
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 3,
					Url = "https://manchester.form.uk.empro.verintcloudservices.com/api/custom?action=bin_checker-get_bin_col_info&actionedby=_KDF_custom&loadform=true&access=citizen&locale=en",
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
				// Parse response content as JSON object
				var responseJson = JsonNode.Parse(clientSideResponse.Content)!.AsObject();
				var binData = responseJson["data"]!.AsObject();

				// Iterate through defined bin types
				var binDays = new List<BinDay>();
				foreach (var binType in binTypes)
				{
					foreach (var key in binType.Keys)
					{
						// Split the date string (e.g., "15/04/2025 00:00:00;\n13/05/2025 00:00:00")
						var rawDates = binData[key]!
							.ToString()
							.Split([";\n", ";"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

						foreach (var rawDate in rawDates)
						{
							// Parse the date string (e.g., "15/04/2025 00:00:00")
							var date = DateOnly.ParseExact(
								rawDate,
								"dd/MM/yyyy HH:mm:ss",
								CultureInfo.InvariantCulture,
								DateTimeStyles.None
							);

							var binDay = new BinDay()
							{
								Date = date,
								Address = address,
								Bins = new List<Bin> { binType }.AsReadOnly()
							};

							binDays.Add(binDay);
						}
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
