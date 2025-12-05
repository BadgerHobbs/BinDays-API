namespace BinDays.Api.Collectors.Collectors.Vendors
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
	/// Base collector implementation for councils using the FCC Environment platform.
	/// </summary>
	internal abstract partial class FccCollectorBase : GovUkCollectorBase
	{
		/// <summary>
		/// The base URL for the council's waste portal.
		/// </summary>
		protected abstract string BaseUrl { get; }

		/// <summary>
		/// The endpoint path for retrieving collection details.
		/// </summary>
		protected abstract string CollectionDetailsEndpoint { get; }

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		protected abstract ReadOnlyCollection<Bin> BinTypes { get; }

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

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting session
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = BaseUrl,
					Method = "GET",
				};

				var getAddressesResponse = new GetAddressesResponse
				{
					NextClientSideRequest = clientSideRequest
				};

				return getAddressesResponse;
			}
			// Prepare client-side request for getting addresses
			else if (clientSideResponse.RequestId == 1)
			{
				var sessionId = SessionTokenRegex().Match(clientSideResponse.Content).Groups[1].Value;
				var cookie = clientSideResponse.Headers["set-cookie"];

				// Fallback if regex fails (some implementations use cookies more aggressively)
				if (string.IsNullOrEmpty(sessionId) && cookie.Contains("fcc_session_cookie"))
				{
					sessionId = cookie.Split(';')
						.First(x => x.Trim().StartsWith("fcc_session_cookie"))
						.Split('=')[1];
				}

				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>
				{
					{"fcc_session_token", sessionId},
					{"postcode", postcode},
				});

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 2,
					Url = $"{BaseUrl}ajaxprocessor/getaddresses",
					Method = "POST",
					Headers = new Dictionary<string, string>
					{
						{ "x-requested-with", "XMLHttpRequest"},
						{ "content-type", "application/x-www-form-urlencoded; charset=UTF-8"},
						{ "cookie", $"fcc_session_cookie={sessionId}" }
					},
					Body = requestBody,
				};

				var getAddressesResponse = new GetAddressesResponse
				{
					NextClientSideRequest = clientSideRequest
				};

				return getAddressesResponse;
			}
			// Process addresses from response
			else if (clientSideResponse.RequestId == 2)
			{
				var responseJson = JsonSerializer.Deserialize<JsonObject>(clientSideResponse.Content)!;
				var addressesJson = responseJson["addresses"]!.AsObject();
				var addresses = new List<Address>();

				foreach (var property in addressesJson)
				{
					var addressArray = property.Value!.AsArray();
					var address = new Address
					{
						Property = addressArray[1]!.GetValue<string>(),
						Postcode = postcode,
						Uid = addressArray[0]!.GetValue<string>(),
					};
					addresses.Add(address);
				}

				var getAddressesResponse = new GetAddressesResponse
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
			// Prepare client-side request for getting session
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = BaseUrl,
					Method = "GET",
				};

				var getBinDaysResponse = new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Prepare client-side request for getting bin days
			else if (clientSideResponse.RequestId == 1)
			{
				var sessionId = SessionTokenRegex().Match(clientSideResponse.Content).Groups[1].Value;

				// Fallback logic for cookie extraction if regex fails
				if (string.IsNullOrEmpty(sessionId) && clientSideResponse.Headers.TryGetValue("set-cookie", out string? cookie))
				{
					if (cookie.Contains("fcc_session_cookie"))
					{
						sessionId = cookie.Split(';')
							.First(x => x.Trim().StartsWith("fcc_session_cookie"))
							.Split('=')[1];
					}
				}

				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>
				{
					{"fcc_session_token", sessionId},
					{"uprn", address.Uid!},
				});

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 2,
					Url = $"{BaseUrl}{CollectionDetailsEndpoint}",
					Method = "POST",
					Headers = new Dictionary<string, string>
					{
						{ "x-requested-with", "XMLHttpRequest"},
						{ "content-type", "application/x-www-form-urlencoded; charset=UTF-8"},
						{ "cookie", $"fcc_session_cookie={sessionId}" }
					},
					Body = requestBody,
				};

				var getBinDaysResponse = new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Process bin days from response
			else if (clientSideResponse.RequestId == 2)
			{
				var responseJson = JsonSerializer.Deserialize<JsonObject>(clientSideResponse.Content)!;
				var binDaysJson = responseJson["binCollections"]!["tile"]!.AsArray();
				var binDays = new List<BinDay>();

				foreach (var binDayHtml in binDaysJson)
				{
					var html = binDayHtml![0]!.ToString();
					var service = ServiceRegex().Match(html).Groups[1].Value;
					var collectionDateString = DateRegex().Match(html).Groups[1].Value;

					var date = DateOnly.ParseExact(
						collectionDateString.Split(",").Last().Trim(),
						"dd MMMM yyyy",
						CultureInfo.InvariantCulture
					);

					var matchedBins = ProcessingUtilities.GetMatchingBins(BinTypes, service);

					var binDay = new BinDay
					{
						Date = date,
						Address = address,
						Bins = matchedBins
					};
					binDays.Add(binDay);
				}

				var getBinDaysResponse = new GetBinDaysResponse
				{
					BinDays = ProcessingUtilities.ProcessBinDays(binDays)
				};

				return getBinDaysResponse;
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}
	}
}
