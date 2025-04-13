namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System.Collections.ObjectModel;
	using System.Text.Json;
	using System.Text.Json.Nodes;

	/// <summary>
	/// Collector implementation for West Devon Borough Council.
	/// </summary>
	internal sealed class WestDevonBoroughCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "West Devon Borough Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://westdevon.fccenvironment.co.uk/mycollections");

		/// <inheritdoc/>
		public override string GovUkId => "west-devon";

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
			throw new InvalidOperationException($"Invalid client-side request.");
		}

		/// <inheritdoc/>
		public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
		{
			throw new NotImplementedException("GetBinDays not implemented.");
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
