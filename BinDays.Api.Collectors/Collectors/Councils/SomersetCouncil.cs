namespace BinDays.Api.Collectors.Collectors.Councils
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
	/// Collector implementation for Somerset Council.
	/// </summary>
	internal sealed partial class SomersetCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Somerset Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.somerset.gov.uk/bins-recycling-and-waste/check-my-collection-days/");

		/// <inheritdoc/>
		public override string GovUkId => "somerset";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Rubbish",
				Colour = BinColour.Black,
				Keys = new List<string>() { "Rubbish" }.AsReadOnly(),
			},
			new()
			{
				Name = "Recycling",
				Colour = BinColour.Blue,
				Keys = new List<string>() { "Recycling" }.AsReadOnly(),
			},
			new()
			{
				Name = "Food Waste",
				Colour = BinColour.Brown,
				Keys = new List<string>() { "Food" }.AsReadOnly(),
				Type = BinType.Caddy,
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Garden" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <summary>
		/// 256-bit key used for encrypting and decrypting requests.
		/// </summary>
		private const string _key = "F57E76482EE3DC3336495DEDEEF3962671B054FE353E815145E29C5689F72FEC";

		/// <summary>
		/// 128-bit initialization vector used for encrypting and decrypting requests.
		/// </summary>
		private const string _iv = "2CBF4FC35C69B82362D393A4F0B9971A";

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting addresses
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = "https://iweb.itouchvision.com/portal/itouchvision/kmbd/address",
					Method = "POST",
					Headers = new Dictionary<string, string>()
					{
						{ "user-agent", Constants.UserAgent },
						{"content-type", "application/json"},
					},
					Body = Encrypt(JsonSerializer.Serialize(new
					{
						P_POSTCODE = postcode,
						P_LANG_CODE = "EN",
						P_CLIENT_ID = 129,
						P_COUNCIL_ID = 34493
					})),
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
				var decryptedContent = Decrypt(clientSideResponse.Content);
				// Parse response content as JSON array
				using var jsonDoc = JsonDocument.Parse(decryptedContent);

				// Iterate through each address json, and create a new address object
				var addresses = new List<Address>();
				var resultsElement = jsonDoc.RootElement.GetProperty("ADDRESS");
				foreach (var addressElement in resultsElement.EnumerateArray())
				{
					var address = new Address()
					{
						Property = addressElement.GetProperty("FULL_ADDRESS").GetString()!.Trim(),
						Postcode = postcode,
						Uid = addressElement.GetProperty("UPRN").ToString()!.Trim(),
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
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = "https://iweb.itouchvision.com/portal/itouchvision/kmbd/collectionDay",
					Method = "POST",
					Headers = new Dictionary<string, string>()
					{
						{ "user-agent", Constants.UserAgent },
						{ "content-type", "application/json" },
					},
					Body = Encrypt(JsonSerializer.Serialize(new
					{
						P_UPRN = address.Uid,
						P_CLIENT_ID = 129,
						P_COUNCIL_ID = 34493,
						P_LANG_CODE = "EN"
					})),
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
				var decryptedContent = Decrypt(clientSideResponse.Content);
				using var jsonDoc = JsonDocument.Parse(decryptedContent);

				var binDays = new List<BinDay>();
				var resultsElement = jsonDoc.RootElement.GetProperty("collectionDay");
				foreach (var binTypeElement in resultsElement.EnumerateArray())
				{
					var binDescription = binTypeElement.GetProperty("binType").GetString()!;
					var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, binDescription);

					// Collect non-empty date fields and parse them
					var dateStrings = new[]
					{
						binTypeElement.GetProperty("collectionDay").GetString(),
						binTypeElement.GetProperty("followingDay").GetString()
					};

					var dates = dateStrings
						.Where(s => !string.IsNullOrWhiteSpace(s))
						.Select(s => DateOnly.ParseExact(s!, "dd-MM-yyyy", CultureInfo.InvariantCulture))
						.ToList();

					foreach (var date in dates)
					{
						binDays.Add(new BinDay
						{
							Date = date,
							Address = address,
							Bins = matchedBins
						});

						// Food waste is collected with recycling but not listed in the calendar
						if (binDescription == "Recycling")
						{
							binDays.Add(new BinDay
							{
								Date = date,
								Address = address,
								Bins = ProcessingUtilities.GetMatchingBins(_binTypes, "Food Waste")
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
		/// Converts a hexadecimal string to a byte array.
		/// </summary>
		private static byte[] HexToByteArray(string hex)
		{
			if (hex.Length % 2 != 0)
			{
				throw new ArgumentException("Hex string must have an even number of characters.");
			}

			byte[] bytes = new byte[hex.Length / 2];
			for (int i = 0; i < hex.Length; i += 2)
			{
				bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
			}
			return bytes;
		}
		
		/// <summary>
		/// Encrypts a plain text string using AES-256-CBC with custom hex key/IV.
		/// </summary>
		/// <param name="plainText">The string to encrypt.</param>
		/// <returns>The encrypted data as a lowercase hexadecimal string.</returns>
		private static string Encrypt(string plainText)
		{
			byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
			byte[] key = HexToByteArray(_key);
			byte[] iv = HexToByteArray(_iv);
			byte[] encrypted;

			// Create an Aes object with the specified parameters
			using (Aes aesAlg = Aes.Create())
			{
				aesAlg.KeySize = 256;          // Set Key size (explicit for clarity, but Key length handles it)
				aesAlg.Mode = CipherMode.CBC;  // Set CBC mode
				aesAlg.Padding = PaddingMode.PKCS7; // Set PKCS7 padding (default for .NET)
				aesAlg.Key = key;
				aesAlg.IV = iv;

				// Create an encryptor to perform the stream transform
				ICryptoTransform encryptor = aesAlg.CreateEncryptor();

				// Create the streams and perform the encryption
				using (var msEncrypt = new System.IO.MemoryStream())
				using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
				{
					csEncrypt.Write(plainBytes, 0, plainBytes.Length);
					csEncrypt.FlushFinalBlock();
					encrypted = msEncrypt.ToArray();
				}
			}

			// Return the encrypted bytes as a lowercase hexadecimal string for easy transport
			return Convert.ToHexString(encrypted).ToLower();
		}

		/// <summary>
		/// Decrypts a hexadecimal encoded string using AES-256-CBC with custom hex key/IV.
		/// </summary>
		/// <param name="cipherTextHex">The hexadecimal encoded string to decrypt.</param>
		/// <returns>The decrypted plain text string.</returns>
		private static string Decrypt(string cipherTextHex)
		{
			byte[] cipherBytes = Convert.FromHexString(cipherTextHex);
			byte[] key = HexToByteArray(_key);
			byte[] iv = HexToByteArray(_iv);
			string plaintext = null;

			// Create an Aes object with the specified parameters
			using (Aes aesAlg = Aes.Create())
			{
				aesAlg.KeySize = 256;
				aesAlg.Mode = CipherMode.CBC;
				aesAlg.Padding = PaddingMode.PKCS7;
				aesAlg.Key = key;
				aesAlg.IV = iv;

				// Create a decryptor to perform the stream transform
				ICryptoTransform decryptor = aesAlg.CreateDecryptor();

				// Create the streams and perform the decryption
				using (var msDecrypt = new System.IO.MemoryStream(cipherBytes))
				using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
				using (var srDecrypt = new System.IO.StreamReader(csDecrypt))
				{
					// Read the decrypted bytes from the stream
					plaintext = srDecrypt.ReadToEnd();
				}
			}

			return plaintext;
		}
	}
}
