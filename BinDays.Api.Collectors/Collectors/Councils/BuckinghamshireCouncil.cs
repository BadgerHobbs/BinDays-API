namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Buckinghamshire Council.
/// </summary>
internal sealed partial class BuckinghamshireCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Buckinghamshire Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.buckinghamshire.gov.uk/waste-and-recycling/find-out-when-its-your-bin-collection/");

	/// <inheritdoc/>
	public override string GovUkId => "buckinghamshire";

	/// <summary>
	/// North Buckinghamshire Council (Aylesbury Vale) bin types.
	/// Properties without room for wheeled bins are collected in sacks, which the council reports
	/// under a separate service name for the same waste stream.
	/// </summary>
	private static readonly IReadOnlyCollection<Bin> _northBinTypes = [
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Food waste" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Mixed recycling", "Recycling Sacks" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden waste" ],
		},
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "General waste", "Refuse Sacks" ],
		},
	];

	/// <summary>
	/// South Buckinghamshire Council (Chiltern, South Bucks, Wycombe) bin types.
	/// </summary>
	private static readonly IReadOnlyCollection<Bin> _southBinTypes = [
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "Food waste" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Mixed recycling" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Garden waste" ],
		},
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "General waste" ],
		},
		new()
		{
			Name = "Paper and Cardboard",
			Colour = BinColour.Black,
			Keys = [ "Paper and cardboard" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Textiles, Batteries and Electricals",
			Colour = BinColour.White,
			Keys = [ "electricals, batteries and textiles" ],
			Type = BinType.Bag,
		},
	];

	/// <summary>
	/// Gets the bin types for a given address.
	/// </summary>
	private static IReadOnlyCollection<Bin> GetBinTypes(Address address)
	{
		// Aylesbury Vale (North) consistently uses 9-digit UPRNs.
		// South areas (Chiltern, South Bucks, Wycombe) consistently use 11 or 12 digit UPRNs.
		return address.Uid!.Length > 9 ? _southBinTypes : _northBinTypes;
	}

	/// <summary>
	/// Base URL for the iTouch Vision gdsv5 API.
	/// </summary>
	private const string _apiBaseUrl = "https://itouchvision.app/portal/itouchvision/gdsv5/";

	/// <summary>
	/// Content-type sent on the gdsv5 requests (the vendor expects the charset suffix).
	/// </summary>
	private const string _jsonContentType = "application/json; charset=UTF-8";

	// The values below are council-specific gdsv5 configuration, captured from the council's own portal.
	// They are stable per council (analogous to the old ClientId/CouncilId) and only change if the council
	// reconfigures its bin collection form.

	/// <summary>
	/// The iTouch Vision client id for this council.
	/// </summary>
	private const int _clientId = 152;

	/// <summary>
	/// The iTouch Vision council id for this council.
	/// </summary>
	private const int _councilId = 34505;

	/// <summary>
	/// The gdsv5 access key used to obtain a session bearer token.
	/// </summary>
	private const string _accessKey = "FA353FC740600CCE617BE0534D090A8C09AD3DCC";

	/// <summary>
	/// The gdsv5 application alias sent when requesting an access token.
	/// </summary>
	private const string _appAlias = "PORTAL_V5_CL";

	/// <summary>
	/// The web service id for the bin collection lookup.
	/// </summary>
	private const int _wsId = 220;

	/// <summary>
	/// The item id for the bin collection lookup result.
	/// </summary>
	private const int _itemId = 723534;

	/// <summary>
	/// The category id of the bin collection form.
	/// </summary>
	private const int _categoryId = 18428;

	/// <summary>
	/// The id of the bin collection form.
	/// </summary>
	private const int _formId = 2299;

	/// <summary>
	/// The page id within the bin collection form.
	/// </summary>
	private const int _pageId = 52742;

	/// <summary>
	/// The form question id for the selected address.
	/// </summary>
	private const string _addressQuestionId = "666224";

	/// <summary>
	/// The form question id for the hidden selected UPRN.
	/// </summary>
	private const string _uprnQuestionId = "666227";

	/// <summary>
	/// Shared AES Key used by this vendor.
	/// </summary>
	private static readonly byte[] _aesKey = Convert.FromHexString("F57E76482EE3DC3336495DEDEEF3962671B054FE353E815145E29C5689F72FEC");

	/// <summary>
	/// Shared AES IV used by this vendor.
	/// </summary>
	private static readonly byte[] _aesIv = Convert.FromHexString("2CBF4FC35C69B82362D393A4F0B9971A");

	/// <summary>
	/// Regex for parsing the collection date/waste-type cell pairs from the gdsv5 result table.
	/// </summary>
	[GeneratedRegex(@"<td[^>]*>(?<date>[^<]+)</td>\s*<td[^>]*>(?<service>[^<]+)</td>")]
	private static partial Regex CollectionRowRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Step 1: Request an access token
		if (clientSideResponse == null)
		{
			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = CreateAccessTokenRequest(NewSessionId()),
			};

			return getAddressesResponse;
		}
		// Step 1 response: Request the address list for the postcode
		else if (clientSideResponse.RequestId == 1)
		{
			var metadata = BuildTokenMetadata(clientSideResponse);
			var payload = JsonSerializer.Serialize(new
			{
				P_CLIENT_ID = _clientId,
				P_ACCESS_KEY = _accessKey,
				LANG_CODE = "EN",
				P_SEARCH_STRING = postcode,
				P_BLPU_CLASS = "27:29",
				P_SHOW_COUNCIL_LOCATION = 1,
				P_COUNCIL_ID = _councilId,
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_apiBaseUrl}address/getAddressList",
				Method = "GET",
				Headers = BuildHeaders(metadata, Encrypt(payload)),
				Options = new ClientSideOptions { Metadata = metadata },
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Step 2 response: Parse the addresses
		else if (clientSideResponse.RequestId == 2)
		{
			using var jsonDoc = JsonDocument.Parse(Decrypt(clientSideResponse.Content));
			var addressElements = jsonDoc.RootElement.GetProperty("ADDRESS");

			// gdsv5 returns a null ADDRESS rather than an empty array for a postcode it holds no
			// addresses for, so treat that as an empty result instead of letting enumeration throw.
			var addresses = new List<Address>();
			if (addressElements.ValueKind == JsonValueKind.Array)
			{
				// Iterate through each address, and create a new address object
				foreach (var addressElement in addressElements.EnumerateArray())
				{
					var address = new Address
					{
						Property = addressElement.GetProperty("FULL_ADDRESS").GetString()?.Trim(),
						Uid = addressElement.GetProperty("UPRN").GetInt64().ToString(),
						Postcode = postcode,
					};

					addresses.Add(address);
				}
			}

			var getAddressesResponse = new GetAddressesResponse
			{
				Addresses = [.. addresses],
			};

			return getAddressesResponse;
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Step 1: Request an access token
		if (clientSideResponse == null)
		{
			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = CreateAccessTokenRequest(NewSessionId()),
			};

			return getBinDaysResponse;
		}
		// Step 1 response: Request the address details (needed to build the form submission)
		else if (clientSideResponse.RequestId == 1)
		{
			var metadata = BuildTokenMetadata(clientSideResponse);
			var payload = JsonSerializer.Serialize(new
			{
				P_CLIENT_ID = _clientId,
				P_ACCESS_KEY = _accessKey,
				LANG_CODE = "EN",
				P_UPRN = address.Uid,
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_apiBaseUrl}address/getAddressDetails",
				Method = "GET",
				Headers = BuildHeaders(metadata, Encrypt(payload)),
				Options = new ClientSideOptions { Metadata = metadata },
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Step 2 response: Submit the form (saveqadata) to mint a report id for the selected address
		else if (clientSideResponse.RequestId == 2)
		{
			var metadata = new Dictionary<string, string>(clientSideResponse.Options.Metadata);

			using var jsonDoc = JsonDocument.Parse(Decrypt(clientSideResponse.Content));
			var details = jsonDoc.RootElement.GetProperty("ADDRESS_DETAILS");

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"{_apiBaseUrl}service/saveqadata",
				Method = "POST",
				Headers = BuildHeaders(metadata, pParameter: null),
				Body = Encrypt(BuildSaveFormPayload(details)),
				Options = new ClientSideOptions { Metadata = metadata },
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Step 3 response: Run the web service request for the freshly-minted report id
		else if (clientSideResponse.RequestId == 3)
		{
			var metadata = new Dictionary<string, string>(clientSideResponse.Options.Metadata);

			using var jsonDoc = JsonDocument.Parse(Decrypt(clientSideResponse.Content));
			var reportId = jsonDoc.RootElement.GetProperty("P_REPORT_ID").GetInt64();

			var payload = JsonSerializer.Serialize(new
			{
				P_CLIENT_ID = _clientId,
				P_ACCESS_KEY = _accessKey,
				LANG_CODE = "EN",
				P_ITEM_ID = _itemId,
				P_WS_ID = _wsId,
				P_REPORT_ID = reportId,
				P_INPUT_DATA = new { uprn = address.Uid },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 4,
				Url = $"{_apiBaseUrl}plugin/getWSRResult",
				Method = "GET",
				Headers = BuildHeaders(metadata, Encrypt(payload)),
				Options = new ClientSideOptions { Metadata = metadata },
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Step 4 response: Parse the collection dates from the returned HTML
		else if (clientSideResponse.RequestId == 4)
		{
			var html = ExtractResultHtml(Decrypt(clientSideResponse.Content));
			var binTypes = GetBinTypes(address);

			// Iterate through each collection row (a date paired with a waste type), and create a bin day
			var binDays = new List<BinDay>();
			foreach (Match match in CollectionRowRegex().Matches(html))
			{
				var service = match.Groups["service"].Value.Trim();

				// Sack properties are also told when replacement sacks are delivered. These are not
				// collections, and would otherwise match the sack keys of the bins they belong to.
				if (service.StartsWith("Deliver", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				var matchedBins = ProcessingUtilities.GetMatchingBins(binTypes, service);

				var binDay = new BinDay
				{
					Date = DateUtilities.ParseDateInferringYear(match.Groups["date"].Value.Trim(), "dddd d MMMM"),
					Address = address,
					Bins = matchedBins,
				};

				binDays.Add(binDay);
			}

			var getBinDaysResponse = new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};

			return getBinDaysResponse;
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <summary>
	/// Creates the request for a gdsv5 access token, generating a fresh session id for the cycle.
	/// </summary>
	private static ClientSideRequest CreateAccessTokenRequest(string sessionId)
	{
		var payload = JsonSerializer.Serialize(new
		{
			P_USER_ID = (string?)null,
			P_CLIENT_ID = _clientId,
			P_ACCESS_KEY = _accessKey,
			P_COUNCIL_ID = _councilId,
			P_APP_ALIAS = _appAlias,
		});

		var metadata = new Dictionary<string, string> { { "sid", sessionId } };

		return new ClientSideRequest
		{
			RequestId = 1,
			Url = $"{_apiBaseUrl}util/getAccessToken",
			Method = "GET",
			Headers = BuildHeaders(metadata, Encrypt(payload)),
			Options = new ClientSideOptions { Metadata = metadata },
		};
	}

	/// <summary>
	/// Reads the session id back from the access-token response and adds the decrypted bearer token,
	/// returning the metadata to carry through the remaining requests.
	/// </summary>
	private static Dictionary<string, string> BuildTokenMetadata(ClientSideResponse clientSideResponse)
	{
		var metadata = new Dictionary<string, string>(clientSideResponse.Options.Metadata);

		using var jsonDoc = JsonDocument.Parse(Decrypt(clientSideResponse.Content));
		metadata["token"] = jsonDoc.RootElement.GetProperty("token").GetString()!;

		return metadata;
	}

	/// <summary>
	/// Builds the request headers, including the session id, the bearer token (once obtained), and the
	/// optional encrypted payload header for GET requests.
	/// </summary>
	private static Dictionary<string, string> BuildHeaders(Dictionary<string, string> metadata, string? pParameter)
	{
		var headers = new Dictionary<string, string>
		{
			{ "user-agent", Constants.UserAgent },
			{ "content-type", _jsonContentType },
			{ "sessionid", metadata["sid"] },
		};

		if (metadata.TryGetValue("token", out var token))
		{
			headers["authorization"] = $"Bearer {token}";
		}

		if (pParameter != null)
		{
			headers["p_parameter"] = pParameter;
		}

		return headers;
	}

	/// <summary>
	/// Builds the saveqadata form submission payload for the selected address, using its full details.
	/// </summary>
	private static string BuildSaveFormPayload(JsonElement details)
	{
		string Get(string name) => details.TryGetProperty(name, out var value) ? value.ToString() : string.Empty;

		var locationData = new Dictionary<string, object>
		{
			{ "INCIDENT_LATITUDE", Get("LATITUDE") },
			{ "INCIDENT_LONGITUDE", Get("LONGITUDE") },
			{ "LOCATION", Get("FULL_ADDRESS") },
			{ "REPORT_ADDRESS1", Get("BUILDING_NUMBER") },
			{ "REPORT_ADDRESS2", Get("THOROUGHFARE_NAME") },
			{ "REPORT_ADDRESS3", Get("POST_TOWN") },
			{ "REPORT_CITY", Get("TOWN_NAME") },
			{ "REPORT_STATE", string.Empty },
			{ "REPORT_COUNTRY", "GB" },
			{ "REPORT_PINCODE", Get("POSTCODE") },
			{ "REPORT_TOWN", Get("POST_TOWN") },
			{ "REPORT_UPRN", long.Parse(Get("UPRN"), CultureInfo.InvariantCulture) },
			{ "REPORT_USRN", long.Parse(Get("USRN"), CultureInfo.InvariantCulture) },
			{ "GEO_LOCATION_TYPE", "ROOFTOP" },
			{ "GEO_TYPES", "street_address" },
			{ "LPI_KEY", Get("LPI_KEY") },
			{ "IS_MANUAL_ADDR", 0 },
		};

		return JsonSerializer.Serialize(new
		{
			P_ACCESS_KEY = _accessKey,
			P_APP_ID = 0,
			P_REPORT_ID = (long?)null,
			P_USER_ID = (string?)null,
			P_CATEGORY_ID = _categoryId,
			P_CLIENT_ID = _clientId,
			P_COUNCIL_ID = _councilId,
			P_FORM_ID = _formId,
			LANG_CODE = "EN",
			P_ALLOW_START_PAGE = 0,
			P_SKIPPED_PAGE_ID = string.Empty,
			P_PAGE_ID = _pageId,
			P_REPORT_DATA = new
			{
				REPORT_DATA = new object[]
				{
					new { ANSWER = new { ID = string.Empty }, QUESTION = new { VALUE = "honeypot" } },
					new { ANSWER = new { ID = string.Empty, VALUE = new { LOCATION_DATA = locationData } }, QUESTION = new { ID = _addressQuestionId, VALUE = " Your address" } },
					new { ANSWER = new { ID = string.Empty, VALUE = "UPRN='#REPORT_UPRN#'" }, QUESTION = new { ID = _uprnQuestionId, VALUE = "-Hidden- Selected UPRN" } },
				},
			},
		});
	}

	/// <summary>
	/// Extracts the collection-schedule HTML from the web service request result.
	/// </summary>
	private static string ExtractResultHtml(string wsrResultJson)
	{
		using var jsonDoc = JsonDocument.Parse(wsrResultJson);
		var outputData = jsonDoc.RootElement.GetProperty("WSR_VALUE").GetProperty("OUTPUT_DATA");

		// Concatenate every output item's HTML value, so the collection table is found regardless of order
		var html = new StringBuilder();
		foreach (var item in outputData.EnumerateArray())
		{
			if (item.TryGetProperty("VAL", out var value) && value.ValueKind == JsonValueKind.String)
			{
				html.Append(value.GetString());
			}
		}

		return html.ToString();
	}

	/// <summary>
	/// Generates a fresh random session id for a request cycle.
	/// </summary>
	private static string NewSessionId()
	{
		return Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
	}

	/// <summary>
	/// Encrypts a plain text string using AES-256-CBC with custom hex key/IV.
	/// </summary>
	private static string Encrypt(string plainText)
	{
		using var aesAlg = Aes.Create();
		aesAlg.Key = _aesKey;
		aesAlg.IV = _aesIv;
		aesAlg.Mode = CipherMode.CBC;
		aesAlg.Padding = PaddingMode.PKCS7;
		var encryptedBytes = aesAlg.EncryptCbc(Encoding.UTF8.GetBytes(plainText), _aesIv);

		return Convert.ToHexString(encryptedBytes).ToLowerInvariant();
	}

	/// <summary>
	/// Decrypts a hexadecimal encoded string using AES-256-CBC with custom hex key/IV.
	/// </summary>
	private static string Decrypt(string hex)
	{
		using var aesAlg = Aes.Create();
		aesAlg.Key = _aesKey;
		aesAlg.IV = _aesIv;
		aesAlg.Mode = CipherMode.CBC;
		aesAlg.Padding = PaddingMode.PKCS7;
		var decryptedBytes = aesAlg.DecryptCbc(Convert.FromHexString(hex), _aesIv);

		return Encoding.UTF8.GetString(decryptedBytes);
	}
}
