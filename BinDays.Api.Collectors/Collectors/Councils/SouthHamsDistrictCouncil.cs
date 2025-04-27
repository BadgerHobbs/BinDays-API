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
		private readonly ReadOnlyCollection<Bin> binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Recycling",
				Colour = "Green",
				Keys = new List<string>() { "Recycling" }.AsReadOnly(),
			},
			new()
			{
				Name = "Refuse",
				Colour = "Grey",
				Keys = new List<string>() { "Refuse" }.AsReadOnly(),
			},
			new()
			{
				Name = "Garden Waste",
				Colour = "Brown",
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
				// Get session id from response content
				var sessionId = GetSessionId(clientSideResponse.Content);

				// Prepare client-side request
				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
				{
					{"fcc_session_token", sessionId},
					{"postcode", postcode},
				});

				var requestHeaders = new Dictionary<string, string>() {
					{"x-requested-with", "XMLHttpRequest"},
					{"content-type", "application/x-www-form-urlencoded; charset=UTF-8"},
					{"origin", "https://waste.southhams.gov.uk"},
					{"referer", "https://waste.southhams.gov.uk/"},
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
						Property = fullAddress, // Dart logic extracts property and street, but keeping it simple as per instructions
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
			throw new InvalidOperationException($"Invalid client-side request.");
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
				// Get session id from response content
				var sessionId = GetSessionId(clientSideResponse.Content);

				// Prepare client-side request
				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
				{
					{"fcc_session_token", sessionId},
					{"uprn", address.Uid!},
				});

				var requestHeaders = new Dictionary<string, string>() {
					{"x-requested-with", "XMLHttpRequest"},
					{"content-type", "application/x-www-form-urlencoded; charset=UTF-8"},
					{"origin", "https://waste.southhams.gov.uk"},
					{"referer", "https://waste.southhams.gov.uk/"},
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

					if (!serviceMatch.Success || !dateMatch.Success)
					{
						continue; // Skip if regex fails to find service or date
					}

					var service = serviceMatch.Groups[1].Value;
					var collectionDateString = dateMatch.Groups[1].Value;

					// Get matching bin types from the service using the keys
					var matchedBinTypes = this.binTypes.Where(x => x.Keys.Any(y => service.Contains(y)));

					// Parse the date (e.g. 'Tuesday, 15 April 2025') to date only
					// The legacy code splits by ',' and takes the last part.
					var datePart = collectionDateString.Split(",").Last().Trim();
					if (!DateTime.TryParseExact(
						datePart,
						"d MMMM yyyy", // Legacy format from Dart code
						CultureInfo.InvariantCulture,
						DateTimeStyles.None,
						out var date))
					{
						continue; // Skip if date parsing fails
					}

					var binDay = new BinDay()
					{
						Date = DateOnly.FromDateTime(date),
						Address = address,
						Bins = matchedBinTypes.ToList().AsReadOnly()
					};

					binDays.Add(binDay);
				}

				// Merge the bin days
				binDays = [.. ProcessingUtilities.MergeBinDays(binDays)];

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = binDays.AsReadOnly(),
					NextClientSideRequest = null
				};

				return getBinDaysResponse;
			}

			// Throw exception for invalid request
			throw new InvalidOperationException($"Invalid client-side request.");
		}

		/// <summary>
		/// Retrieves the session ID from the HTML content.
		/// </summary>
		/// <param name="htmlContent">The HTML content of the page.</param>
		/// <returns>The session ID.</returns>
		/// <exception cref="InvalidOperationException">Thrown if the session token cannot be found.</exception>
		private static string GetSessionId(string htmlContent)
		{
			var match = SessionTokenRegex().Match(htmlContent);
			if (match.Success)
			{
				return match.Groups[1].Value;
			}

			throw new InvalidOperationException("Could not extract session token from initial page load.");
		}
	}
}