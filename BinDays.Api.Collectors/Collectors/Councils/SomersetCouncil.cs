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
		/// Client ID for Buckinghamshire Council requests.
		/// </summary>
		private const int _clientId = 129;

		/// <summary>
		/// Council ID for Buckinghamshire Council requests.
		/// </summary>
		private const int _councilId = 34493;

		/// <summary>
		/// Base URL for the Buckinghamshire Council API.
		/// </summary>
		private const string _apiBaseUrl = "https://iweb.itouchvision.com/portal/itouchvision/";

		/// <summary>
		/// AES key used for encrypting and decrypting requests.
		/// </summary>
		private static readonly byte[] _aesKey = Convert.FromHexString("F57E76482EE3DC3336495DEDEEF3962671B054FE353E815145E29C5689F72FEC");

		/// <summary>
		/// AES IV used for encrypting and decrypting requests.
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
					P_CLIENT_ID = _clientId,
					P_COUNCIL_ID = _councilId
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = $"{_apiBaseUrl}kmbd/address",
					Method = "POST",
					Headers = new Dictionary<string, string>()
					{
						{ "user-agent", Constants.UserAgent },
						{"content-type", "application/json; charset=UTF-8"},
					},
					Body = Encrypt(JsonSerializer.Serialize(payload)),
				};

				return new GetAddressesResponse()
				{
					NextClientSideRequest = clientSideRequest
				};
			}
			// Process addresses from response
			else if (clientSideResponse.RequestId == 1)
			{
				string decryptedContent = Decrypt(clientSideResponse.Content);
				// Parse response content as JSON array
				using var jsonDoc = JsonDocument.Parse(decryptedContent);

				var addresses = new List<Address>();
				var resultsElement = jsonDoc.RootElement.GetProperty("ADDRESS");
				foreach (var addressElement in resultsElement.EnumerateArray())
				{
					var address = new Address()
					{
						Property = addressElement.GetProperty("FULL_ADDRESS").GetString()!.Trim(),
						Postcode = postcode,
						Uid = addressElement.GetProperty("UPRN").GetInt64().ToString(),
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
					P_CLIENT_ID = _clientId,
					P_COUNCIL_ID = _councilId,
					P_LANG_CODE = "EN"
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = $"{_apiBaseUrl}kmbd/collectionDay",
					Method = "POST",
					Headers = new Dictionary<string, string>()
					{
						{ "user-agent", Constants.UserAgent },
						{ "content-type", "application/json; charset=UTF-8" },
					},
					Body = Encrypt(JsonSerializer.Serialize(payload)),
				};

				return new GetBinDaysResponse()
				{
					NextClientSideRequest = clientSideRequest
				};
			}
			// Process bin days from response
			else if (clientSideResponse.RequestId == 1)
			{
				string decryptedContent = Decrypt(clientSideResponse.Content);
				using var jsonDoc = JsonDocument.Parse(decryptedContent);

				var binDays = new List<BinDay>();
				var collectionDayArray = jsonDoc.RootElement.GetProperty("collectionDay");

				foreach (var collectionItem in collectionDayArray.EnumerateArray())
				{
					var binDescription = collectionItem.GetProperty("binType").GetString()!;
					var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, binDescription);

					// Collect non-empty date fields and parse them
					var dateStrings = new[]
					{
						collectionItem.GetProperty("collectionDay").GetString(),
						collectionItem.GetProperty("followingDay").GetString()
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

				return new GetBinDaysResponse()
				{
					BinDays = ProcessingUtilities.ProcessBinDays(binDays),
				};
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
