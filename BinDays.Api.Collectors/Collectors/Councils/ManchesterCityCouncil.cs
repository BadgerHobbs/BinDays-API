namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors.Vendors;
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
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
		private readonly IReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Paper & Card",
				Colour = BinColour.Blue,
				Keys = ["ahtm_dates_blue_pulpable_bin"],
			},
			new ()
			{
				Name = "Metal, Glass & Plastic Bottles",
				Colour = BinColour.Brown,
				Keys = ["ahtm_dates_brown_commingled_bin"],
			},
			new()
			{
				Name = "Food & Garden Waste",
				Colour = BinColour.Green,
				Keys = ["ahtm_dates_green_organic_bin"],
			},
			new()
			{
				Name = "General Waste",
				Colour = BinColour.Black,
				Keys = ["ahtm_dates_black_bin"],
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
				};

				var getAddressesResponse = new GetAddressesResponse()
				{
					NextClientSideRequest = clientSideRequest
				};

				return getAddressesResponse;
			}
			// Prepare client-side request for getting addresses
			else if (clientSideResponse?.RequestId == 1)
			{
				// Get authorization token from response header
				var authToken = clientSideResponse.Headers["authorization"];

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
			// Prepare client-side request for getting authorization token
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = "https://manchester.form.uk.empro.verintcloudservices.com/api/citizen?archived=Y&preview=false&locale=en",
					Method = "GET",
				};

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Prepare client-side request for getting property details (UPRN)
			else if (clientSideResponse?.RequestId == 1)
			{
				// Get authorization token from response header
				var authToken = clientSideResponse.Headers["authorization"];

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
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Prepare client-side request for getting bin collection dates
			else if (clientSideResponse?.RequestId == 2)
			{
				// Get authorization token from response header
				var authToken = clientSideResponse.Headers["authorization"];

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
				foreach (var binType in _binTypes)
				{
					foreach (var key in binType.Keys)
					{
						// Split the date string (e.g. "15/04/2025 00:00:00;\n13/05/2025 00:00:00")
						var rawDates = binData[key]!
							.ToString()
							.Split([";\n", ";"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

						foreach (var rawDate in rawDates)
						{
							// Parse the date string (e.g. "15/04/2025 00:00:00")
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
