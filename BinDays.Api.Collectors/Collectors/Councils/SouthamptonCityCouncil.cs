namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Globalization;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Collector implementation for Solihull Metropolitan Borough Council.
	/// </summary>
	internal sealed partial class SouthamptonCityCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Southampton City Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.southampton.gov.uk/bins-recycling/bins/");

		/// <inheritdoc/>
		public override string GovUkId => "southampton";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Recycling",
				Colour = BinColor.Blue,
				Keys = new List<string>() { "Recycling" }.AsReadOnly(),
			},
			new()
			{
				Name = "Glass",
				Colour = BinColor.Grey,
				Keys = new List<string>() { "Glass" }.AsReadOnly(),
			},
			new()
			{
				Name = "General",
				Colour = BinColor.Green,
				Keys = new List<string>() { "General" }.AsReadOnly(),
			},
			new()
			{
				Name = "Garden",
				Colour = BinColor.Brown,
				Keys = new List<string>() { "Garden" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <summary>
		/// Regex for the ufprt token values from input fields.
		/// </summary>
		[GeneratedRegex(@"<input[^>]*?(?:name|id)=[""']ufprt[""'][^>]*?value=[""'](?<ufprt>[^""']*)[""'][^>]*?/?>")]
		private static partial Regex UfprtTokenRegex();

		/// <summary>
		/// Regex for the __RequestVerificationToken token values from input fields.
		/// </summary>
		[GeneratedRegex(@"<input[^>]*?(?:name|id)=[""']__RequestVerificationToken[""'][^>]*?value=[""'](?<token>[^""']*)[""'][^>]*?/?>")]
		private static partial Regex RequestVerificationTokenRegex();

		/// <summary>
		/// Regex for the addresses from the options elements.
		/// </summary>
		[GeneratedRegex(@"<option\s+value=""(?<uid>\d+),""[^>]*>\s*(?<address>.*?)\s*</option>")]
		private static partial Regex AddressesRegex();

		/// <summary>
		/// Regex for the bin days from the data table elements.
		/// </summary>
		[GeneratedRegex(@"\{title:\s*'<img[^>]*?alt=""(?<binType>[^""]+)""[^>]*>',\s*start:\s*'(?<collectionDate>\d{1,2}\/\d{1,2}\/\d{4})\s+\d{1,2}:\d{2}:\d{2}\s+[AP]M'\}")]
		private static partial Regex BinDaysRegex();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting token
			if (clientSideResponse == null)
			{
				// Prepare client-side request
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = "https://www.southampton.gov.uk/bins-recycling/bins/collections/",
					Method = "GET",
					Headers = new Dictionary<string, string>() {
						{"user-agent", Constants.UserAgent},
					},
				};

				var getAddressesResponse = new GetAddressesResponse()
				{
					Addresses = null,
					NextClientSideRequest = clientSideRequest
				};

				return getAddressesResponse;
			}
			// Prepare client-side request for getting addresses
			else if (clientSideResponse.RequestId == 1)
			{
				var ufprtMatch = UfprtTokenRegex().Match(clientSideResponse.Content);
				var requestVerificationTokenMatch = RequestVerificationTokenRegex().Match(clientSideResponse.Content);
				if (!ufprtMatch.Success)
				{
					throw new InvalidOperationException("Could not find required 'ufprt' token for address lookup.");
				}
				if (!requestVerificationTokenMatch.Success)
				{
					throw new InvalidOperationException("Could not find required '__RequestVerificationToken' for address lookup.");
				}
				var ufprt = ufprtMatch.Groups["ufprt"].Value;
				var requestVerificationToken = requestVerificationTokenMatch.Groups["token"].Value;

				// Prepare client-side request
				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
				{
					{"SearchString", postcode},
					{"ufprt", ufprt},
					{"__RequestVerificationToken", requestVerificationToken}
				});

				var requestHeaders = new Dictionary<string, string>() {
					{"user-agent", Constants.UserAgent},
					{"content-type", "application/x-www-form-urlencoded"},
					{"cookie", ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["Set-Cookie"])},
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = "https://www.southampton.gov.uk/bins-recycling/bins/collections/",
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
			else if (clientSideResponse.RequestId == 2)
			{
				// Get addresses from response
				var rawAddresses = AddressesRegex().Matches(clientSideResponse.Content)!;

				// Iterate through each address, and create a new address object
				var addresses = new List<Address>();
				foreach (Match rawAddress in rawAddresses)
				{
					var property = rawAddress.Groups["address"].Value;
					var uprn = rawAddress.Groups["uid"].Value;

					var address = new Address()
					{
						Property = property,
						Street = string.Empty,
						Town = string.Empty,
						Postcode = postcode,
						Uid = uprn,
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
			// Prepare client-side request for getting token
			if (clientSideResponse == null)
			{
				// Prepare client-side request
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = "https://www.southampton.gov.uk/bins-recycling/bins/collections/",
					Method = "GET",
					Headers = new Dictionary<string, string>() {
						{"user-agent", Constants.UserAgent},
					},
				};

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = null,
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Prepare client-side request for getting bin days
			else if (clientSideResponse.RequestId == 1)
			{
				// Prepare client-side request
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = $"https://www.southampton.gov.uk/whereilive/waste-calendar?UPRN={address.Uid}",
					Method = "GET",
				};

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = null,
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Process bin days from response
			else if (clientSideResponse.RequestId == 2)
			{
				// Get bin days from response
				var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content)!;

				// Iterate through each bin day, and create a new bin day object
				var binDays = new List<BinDay>();
				foreach (Match rawBinDay in rawBinDays)
				{
					var service = rawBinDay.Groups["binType"].Value;
					var collectionDate = rawBinDay.Groups["collectionDate"].Value;

					// Parse the collection date (6/19/2025)
					var date = DateOnly.ParseExact(
						collectionDate,
						"M/d/yyyy",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);

					// Get matching bin types from the service using the keys
					var matchedBinTypes = _binTypes.Where(x => x.Keys.Any(y => service.Contains(y)));

					var binDay = new BinDay()
					{
						Date = date,
						Address = address,
						Bins = matchedBinTypes.ToList().AsReadOnly()
					};

					binDays.Add(binDay);
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
