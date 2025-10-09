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
	using System.Text.RegularExpressions;

	/// <summary>
	/// Collector implementation for Torridge District Council.
	/// </summary>
	internal sealed partial class TorridgeDistrictCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Torridge District Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.torridge.gov.uk/forms/firmstep/wastecalendar.html");

		/// <inheritdoc/>
		public override string GovUkId => "torridge";

		/// <summary>
		/// Regex to extract the session ID (sid) from HTML content.
		/// </summary>
		[GeneratedRegex(@"sid=([a-f0-9]+)")]
		private static partial Regex SessionIdRegex();

		/// <summary>
		/// Regex to parse the bin schedule string, e.g., "Refuse: Today then every alternate Mon".
		/// </summary>
		[GeneratedRegex(@"^([^:]+):\s*(.*?)\s*then", RegexOptions.IgnoreCase)]
		private static partial Regex BinScheduleRegex();

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> binTypes = new List<Bin>()
		{
			new()
			{
				Name = "General Waste",
				Colour = BinColour.Black,
				Keys = new List<string>() { "Refuse" }.AsReadOnly(),
			},
			new()
			{
				Name = "Recycling",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Recycling" }.AsReadOnly(),
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Green,
				Keys = new List<string>() { "GardenBin" }.AsReadOnly(),
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
					Url = "https://torridgedc-self.achieveservice.com/service/My_property_information",
					Method = "GET",
					Headers = [],
					Body = string.Empty,
				};

				return new GetAddressesResponse()
				{
					Addresses = null,
					NextClientSideRequest = clientSideRequest
				};
			}

			// Step 2: Get Addresses using Session ID and Postcode
			if (clientSideResponse.RequestId == 1)
			{
				// Get set-cookies from response
				var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);

				// Extract Session ID from Step 1 response content
				var sessionId = SessionIdRegex().Match(clientSideResponse.Content).Groups[1].Value;

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = $"https://torridgedc-self.achieveservice.com/apibroker/runLookup?id=5a3aefaf052d9&sid={sessionId}",
					Method = "POST",
					Headers = new Dictionary<string, string>()
					{
						{ "cookie", requestCookies },
					},
					Body = JsonSerializer.Serialize(new
					{
						formValues = new
						{
							Search = new
							{
								postcode_search = new
								{
									value = postcode,
								},
							},
						},
					}),
				};

				return new GetAddressesResponse()
				{
					Addresses = null,
					NextClientSideRequest = clientSideRequest
				};
			}

			// Step 3: Process Addresses from Response
			if (clientSideResponse.RequestId == 2)
			{
				// Parse response content as JSON object, failing early if the structure is wrong.
				var responseJson = JsonDocument.Parse(clientSideResponse.Content).RootElement;
				var rawAddresses = responseJson.GetProperty("integration").GetProperty("transformed").GetProperty("rows_data");

				// Iterate through each address object
				var addresses = new List<Address>();
				foreach (var property in rawAddresses.EnumerateObject())
				{
					var addressData = property.Value;
					var address = new Address()
					{
						Property = addressData.GetProperty("display").GetString(),
						Street = string.Empty,
						Town = string.Empty,
						Postcode = postcode,
						Uid = addressData.GetProperty("uprn").GetString(),
					};

					addresses.Add(address);
				}

				return new GetAddressesResponse()
				{
					Addresses = addresses.AsReadOnly(),
					NextClientSideRequest = null
				};
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}

		/// <inheritdoc/>
		public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
		{
			// Step 1: Get Session ID
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = "https://torridgedc-self.achieveservice.com/service/My_property_information",
					Method = "GET",
					Headers = [],
					Body = string.Empty,
				};

				return new GetBinDaysResponse()
				{
					BinDays = null,
					NextClientSideRequest = clientSideRequest
				};
			}

			// Step 2: Get Bin Days using Session ID and UPRN
			if (clientSideResponse.RequestId == 1)
			{
				// Get set-cookies from response
				var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);

				// Extract Session ID from Step 1 response content
				var sessionId = SessionIdRegex().Match(clientSideResponse.Content).Groups[1].Value;

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = $"https://torridgedc-self.achieveservice.com/apibroker/runLookup?id=6583107397653&sid={sessionId}",
					Method = "POST",
					Headers = new Dictionary<string, string>()
					{
						{ "cookie", requestCookies },
					},
					Body = JsonSerializer.Serialize(new
					{
						formValues = new
						{
							Search = new
							{
								postcode_search = new { value = address.Postcode },
								yourAddress = new { value = address.Uid },
								uprn = new { value = address.Uid },
							},
						},
					}),
				};

				return new GetBinDaysResponse()
				{
					BinDays = null,
					NextClientSideRequest = clientSideRequest
				};
			}

			// Step 3: Process Bin Days from Response
			if (clientSideResponse.RequestId == 2)
			{
				// Parse response content as JSON object, failing early if the structure is wrong.
				var responseJson = JsonDocument.Parse(clientSideResponse.Content).RootElement;

				var rawBinDayData = responseJson
					.GetProperty("integration")
					.GetProperty("transformed")
					.GetProperty("rows_data")
					.GetProperty("0");

				var binDays = new List<BinDay>();

				// Iterate through each collection entry (e.g., Round1, Round2)
				foreach (var property in rawBinDayData.EnumerateObject())
				{
					var scheduleString = property.Value.GetString();
					var match = BinScheduleRegex().Match(scheduleString!);

					var binKey = match.Groups[1].Value.Trim();
					var datePart = match.Groups[2].Value.Split('(')[0].Trim();

					// Skip if no date as optional garden waste
					if (string.IsNullOrEmpty(datePart))
					{
						continue;
					}

					DateOnly collectionDate;
					if (datePart.Equals("Today", StringComparison.OrdinalIgnoreCase))
					{
						collectionDate = DateOnly.FromDateTime(DateTime.Today);
					}
					else if (datePart.Equals("Tomorrow", StringComparison.OrdinalIgnoreCase))
					{
						collectionDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
					}
					else
					{
						// e.g., "Wed 16 Jul". The year is implied as the current one.
						collectionDate = DateOnly.ParseExact(datePart, "ddd d MMM", CultureInfo.InvariantCulture);
					}

					var matchedBins = binTypes.Where(bin => bin.Keys.Contains(binKey)).ToList();
					if (matchedBins.Any())
					{
						binDays.Add(new BinDay
						{
							Date = collectionDate,
							Address = address,
							Bins = matchedBins.AsReadOnly(),
						});
					}
				}

				return new GetBinDaysResponse()
				{
					BinDays = ProcessingUtilities.ProcessBinDays(binDays),
					NextClientSideRequest = null
				};
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}
	}
}
