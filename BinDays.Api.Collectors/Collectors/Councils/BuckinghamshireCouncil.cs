namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Globalization;
	using System.Linq;
	using System.Security.Cryptography;
	using System.Text;
	using System.Text.Json;

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
		/// The list of bin types for this collector.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Food Waste",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Food waste" }.AsReadOnly(),
				Type = BinType.Caddy,
			},
			new()
			{
				Name = "Mixed Recycling",
				Colour = BinColour.Blue,
				Keys = new List<string>() { "Mixed recycling" }.AsReadOnly(),
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Brown,
				Keys = new List<string>() { "Garden waste" }.AsReadOnly(),
			},
			new()
			{
				Name = "General Waste",
				Colour = BinColour.Green,
				Keys = new List<string>() { "General waste" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <summary>
		/// Client ID for Buckinghamshire Council requests.
		/// </summary>
		private const int _bucksClientId = 152;

		/// <summary>
		/// Council ID for Buckinghamshire Council requests.
		/// </summary>
		private const int _bucksCouncilId = 34505;

		/// <summary>
		/// Base URL for the Buckinghamshire Council API.
		/// </summary>
		private const string _apiBaseUrl = "https://itouchvision.app/portal/itouchvision/";

		/// <summary>
		/// AES key used for encrypting and decrypting requests.
		/// </summary>
		public static readonly byte[] _aesKey = Convert.FromHexString("F57E76482EE3DC3336495DEDEEF3962671B054FE353E815145E29C5689F72FEC");

		/// <summary>
		/// AES IV used for encrypting and decrypting requests.
		/// </summary>
		public static readonly byte[] _aesIv = Convert.FromHexString("2CBF4FC35C69B82362D393A4F0B9971A");

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting addresses
			if (clientSideResponse == null)
			{
				var payload = new
				{
					P_POSTCODE = postcode,
					P_LANG_CODE = "EN",
					P_CLIENT_ID = _bucksClientId,
					P_COUNCIL_ID = _bucksCouncilId
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = $"{_apiBaseUrl}kmbd/address",
					Method = "POST",
					Headers = new Dictionary<string, string>
					{
						{ "Content-type", "application/json; charset=UTF-8" },
					},
					Body = EncryptRequestJson(JsonSerializer.Serialize(payload)),
				};

				return new GetAddressesResponse()
				{
					NextClientSideRequest = clientSideRequest
				};
			}
			// Process addresses from response
			else if (clientSideResponse.RequestId == 1)
			{
				var addresses = new List<Address>();

				string decryptedJson = DecryptResponseJson(clientSideResponse.Content);
				using var jsonDoc = JsonDocument.Parse(decryptedJson);

				// Iterate through each address json, and create a new address object
				foreach (var addressElement in jsonDoc.RootElement.GetProperty("ADDRESS").EnumerateArray())
				{
					var address = new Address()
					{
						Property = addressElement.GetProperty("FULL_ADDRESS").GetString()?.Trim(),
						Uid = addressElement.GetProperty("UPRN").GetInt64().ToString(),
						Postcode = postcode,
					};

					addresses.Add(address);
				}

				return new GetAddressesResponse()
				{
					Addresses = addresses.AsReadOnly(),
				};
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}

		/// <inheritdoc/>
		public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting bin days
			if (clientSideResponse == null)
			{
				var payload = new
				{
					P_UPRN = address.Uid,
					P_CLIENT_ID = _bucksClientId,
					P_COUNCIL_ID = _bucksCouncilId,
					P_LANG_CODE = "EN"
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = $"{_apiBaseUrl}kmbd/collectionDay",
					Method = "POST",
					Headers = new Dictionary<string, string>
					{
						{ "Content-type", "application/json; charset=UTF-8" },
					},
					Body = EncryptRequestJson(JsonSerializer.Serialize(payload)),
				};

				return new GetBinDaysResponse()
				{
					NextClientSideRequest = clientSideRequest
				};
			}
			// Process bin days from response
			else if (clientSideResponse.RequestId == 1)
			{
				var binDays = new List<BinDay>();

				string decryptedJson = DecryptResponseJson(clientSideResponse.Content);
				using var jsonDoc = JsonDocument.Parse(decryptedJson);
				var collectionDayArray = jsonDoc.RootElement.GetProperty("collectionDay");

				foreach (var collectionItem in collectionDayArray.EnumerateArray())
				{
					var rawBinType = collectionItem.GetProperty("binType").GetString()!;
					var matchedBins = _binTypes.Where(bin => bin.Keys.Contains(rawBinType)).ToList().AsReadOnly();

					// Process the main collection day
					var collectionDayString = collectionItem.GetProperty("collectionDay").GetString()!;
					var date = DateOnly.ParseExact(
						collectionDayString,
						"dd-MM-yyyy",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);

					binDays.Add(new BinDay()
					{
						Date = date,
						Address = address,
						Bins = matchedBins
					});

					// Process the following collection day (if it exists)
					var followingDayString = collectionItem.GetProperty("followingDay").GetString()!;
					if (followingDayString == null)
					{
						continue;
					}

					var followingDate = DateOnly.ParseExact(
						followingDayString,
						"dd-MM-yyyy",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);

					binDays.Add(new BinDay()
					{
						Date = followingDate,
						Address = address,
						Bins = matchedBins
					});
				}

				return new GetBinDaysResponse()
				{
					BinDays = ProcessingUtilities.ProcessBinDays(binDays),
				};
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}

		/// <summary>
		/// Encrypts the provided JSON string using AES encryption.
		/// </summary>
		/// <param name="json">The JSON string to encrypt.</param>
		/// <returns>The encrypted hex string.</returns>
		public static string EncryptRequestJson(string json)
		{
			byte[] utf8Data = Encoding.UTF8.GetBytes(json);

			using Aes aesAlg = Aes.Create();
			aesAlg.Key = _aesKey;
			aesAlg.IV = _aesIv;
			aesAlg.Mode = CipherMode.CBC;
			aesAlg.Padding = PaddingMode.PKCS7;

			byte[] encryptedBytes = aesAlg.EncryptCbc(utf8Data, _aesIv);

			return Convert.ToHexString(encryptedBytes).ToLowerInvariant();
		}

		/// <summary>
		/// Decrypts the provided JSON string using AES decryption.
		/// </summary>
		/// <param name="hex">The encrypted hex string to decrypt.</param>
		/// <returns>The decrypted JSON string.</returns>
		public static string DecryptResponseJson(string hex)
		{
			byte[] encryptedBytes = Convert.FromHexString(hex);

			using Aes aesAlg = Aes.Create();
			aesAlg.Key = _aesKey;
			aesAlg.IV = _aesIv;
			aesAlg.Mode = CipherMode.CBC;
			aesAlg.Padding = PaddingMode.PKCS7;

			byte[] decryptedBytes = aesAlg.DecryptCbc(encryptedBytes, _aesIv);

			return Encoding.UTF8.GetString(decryptedBytes);
		}
	}
}
