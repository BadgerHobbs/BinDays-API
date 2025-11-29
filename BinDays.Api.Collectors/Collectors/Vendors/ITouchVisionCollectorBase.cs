namespace BinDays.Api.Collectors.Collectors.Vendors
{
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Globalization;
	using System.Security.Cryptography;
	using System.Text;
	using System.Text.Json;

	/// <summary>
	/// Base collector implementation for councils using the iTouch Vision platform.
	/// </summary>
	internal abstract class ITouchVisionCollectorBase : GovUkCollectorBase
	{
		/// <summary>
		/// The Client ID specific to the council.
		/// </summary>
		protected abstract int ClientId { get; }

		/// <summary>
		/// The Council ID specific to the council.
		/// </summary>
		protected abstract int CouncilId { get; }

		/// <summary>
		/// The base URL for the iTouch Vision API.
		/// </summary>
		protected abstract string ApiBaseUrl { get; }

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		protected abstract ReadOnlyCollection<Bin> BinTypes { get; }

		/// <summary>
		/// Shared AES Key used by this vendor.
		/// </summary>
		private static readonly byte[] _aesKey = Convert.FromHexString("F57E76482EE3DC3336495DEDEEF3962671B054FE353E815145E29C5689F72FEC");

		/// <summary>
		/// Shared AES IV used by this vendor.
		/// </summary>
		private static readonly byte[] _aesIv = Convert.FromHexString("2CBF4FC35C69B82362D393A4F0B9971A");

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
					P_CLIENT_ID = ClientId,
					P_COUNCIL_ID = CouncilId
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = $"{ApiBaseUrl}kmbd/address",
					Method = "GET",
					Headers = new Dictionary<string, string>
					{
						{ "P_PARAMETER", Encrypt(JsonSerializer.Serialize(payload)) }
					},
				};

				var getAddressesResponse = new GetAddressesResponse()
				{
					NextClientSideRequest = clientSideRequest
				};

				return getAddressesResponse;
			}
			// Process addresses from response
			else if (clientSideResponse.RequestId == 1)
			{
				string decryptedJson = Decrypt(clientSideResponse.Content);
				using var jsonDoc = JsonDocument.Parse(decryptedJson);

				var adressElements = jsonDoc.RootElement.GetProperty("ADDRESS");
				var addresses = new List<Address>();

				foreach (var addressElement in adressElements.EnumerateArray())
				{
					var address = new Address()
					{
						Property = addressElement.GetProperty("FULL_ADDRESS").GetString()?.Trim(),
						Uid = addressElement.GetProperty("UPRN").GetInt64().ToString(),
						Postcode = postcode,
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
			// Prepare client-side request for getting bin days
			if (clientSideResponse == null)
			{
				var payload = new
				{
					P_UPRN = address.Uid,
					P_CLIENT_ID = ClientId,
					P_COUNCIL_ID = CouncilId,
					P_LANG_CODE = "EN"
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = $"{ApiBaseUrl}kmbd/collectionDay",
					Method = "GET",
					Headers = new Dictionary<string, string>
					{
						{ "P_PARAMETER", Encrypt(JsonSerializer.Serialize(payload)) }
					},
				};

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Process bin days from response
			else if (clientSideResponse.RequestId == 1)
			{
				string decryptedJson = Decrypt(clientSideResponse.Content);
				using var jsonDoc = JsonDocument.Parse(decryptedJson);

				var collectionDayArray = jsonDoc.RootElement.GetProperty("collectionDay");
				var binDays = new List<BinDay>();

				foreach (var collectionItem in collectionDayArray.EnumerateArray())
				{
					var binType = collectionItem.GetProperty("binType").GetString()!;
					var matchedBins = ProcessingUtilities.GetMatchingBins(BinTypes, binType);

					var dateStrings = new[]
					{
						collectionItem.GetProperty("collectionDay").GetString(),
						collectionItem.GetProperty("followingDay").GetString()
					};

					foreach (var dateString in dateStrings)
					{
						if (!string.IsNullOrWhiteSpace(dateString))
						{
							var date = DateOnly.ParseExact(
								dateString,
								"dd-MM-yyyy",
								CultureInfo.InvariantCulture,
								DateTimeStyles.None
							);

							binDays.Add(new BinDay
							{
								Date = date,
								Address = address,
								Bins = matchedBins
							});
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

		/// <summary>
		/// Encrypts a plain text string using AES-256-CBC with custom hex key/IV.
		/// </summary>
		/// <param name="plainText">The string to encrypt.</param>
		/// <returns>The encrypted data as a lowercase hexadecimal string.</returns>
		private static string Encrypt(string plainText)
		{
			byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

			using Aes aesAlg = Aes.Create();
			aesAlg.Key = _aesKey;
			aesAlg.IV = _aesIv;
			aesAlg.Mode = CipherMode.CBC;
			aesAlg.Padding = PaddingMode.PKCS7;
			byte[] encryptedBytes = aesAlg.EncryptCbc(plainBytes, _aesIv);

			return Convert.ToHexString(encryptedBytes).ToLowerInvariant();
		}

		/// <summary>
		/// Decrypts a hexadecimal encoded string using AES-256-CBC with custom hex key/IV.
		/// </summary>
		/// <param name="hex">The hexadecimal encoded string to decrypt.</param>
		/// <returns>The decrypted plain text string.</returns>
		private static string Decrypt(string hex)
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
