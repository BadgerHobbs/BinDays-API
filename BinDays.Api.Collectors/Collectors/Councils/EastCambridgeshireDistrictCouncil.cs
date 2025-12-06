namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors.Vendors;
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text.Json;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Collector implementation for East Cambridgeshire District Council.
	/// </summary>
	internal sealed partial class EastCambridgeshireDistrictCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "East Cambridgeshire District Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://eastcambs-self.achieveservice.com/bincollections");

		/// <inheritdoc/>
		public override string GovUkId => "east-cambridgeshire";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly IReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Household Waste",
				Colour = BinColour.Black,
				Keys = new List<string>() { "Black Bag" }.AsReadOnly(),
				Type = BinType.Bag,
			},
			new()
			{
				Name = "Recycling",
				Colour = BinColour.Blue,
				Keys = new List<string>() { "Blue Bin" }.AsReadOnly(),
			},
			new()
			{
				Name = "Garden & Food Waste",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Green or Brown Bin" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <summary>
		/// Regex for parsing addresses from the XML response.
		/// </summary>
		[GeneratedRegex(@"<Row id=.*?<result column=""display"".*?>(?<address>.*?)<\/result>.*?<result column=""uprn"".*?>(?<uprn>\d+)<\/result>.*?<\/Row>")]
		private static partial Regex AddressesRegex();

		/// <summary>
		/// Regex for parsing bin collection data from the HTML response.
		/// </summary>
		[GeneratedRegex(@"<div class=""row collectionsrow"">.*?<img.*?alt=""(?<binType>[^""]+)"">.*?<div class=""col-xs-6 col-sm-6"">(?<date>.*?)<\/div>", RegexOptions.Singleline)]
		private static partial Regex BinDaysRegex();

		/// <summary>
		/// Regex for replacing multiple whitespace characters with a single space.
		/// </summary>
		[GeneratedRegex(@"\s+")]
		private static partial Regex WhitespaceRegex();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting session cookies
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = "https://eastcambs-self.achieveservice.com/bincollections",
					Method = "GET",
					Headers = new Dictionary<string, string> {
						{ "User-Agent", Constants.UserAgent }
					},
				};

				return new GetAddressesResponse
				{
					NextClientSideRequest = clientSideRequest
				};
			}
			// Prepare client-side request for getting addresses
			else if (clientSideResponse.RequestId == 1)
			{
				var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);
				var requestUrl = "https://eastcambs-self.achieveservice.com/apibroker/runLookup?id=5a6b2c8861aaf";

				var requestBody = JsonSerializer.Serialize(new
				{
					formValues = new
					{
						Section_1 = new
						{
							postcode_search = new
							{
								value = postcode,
							},
						},
					},
					isPublished = true,
					formName = "Choose collection address",
					formUri = "sandbox-shared://AF-Process-e38aa672-38b4-4355-a601-0132ab5ef8b7/AF-Stage-361ee725-08c0-49eb-aa18-a44a60cbee92/definition.json"
				});

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 2,
					Url = requestUrl,
					Method = "POST",
					Headers = new Dictionary<string, string>
					{
						{ "Content-Type", "application/json" },
						{ "cookie", requestCookies }
					},
					Body = requestBody,
				};

				return new GetAddressesResponse
				{
					NextClientSideRequest = clientSideRequest
				};
			}
			// Process addresses from response
			else if (clientSideResponse.RequestId == 2)
			{
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
				var xmlData = jsonDoc.RootElement.GetProperty("data").GetString()!;

				// Get addresses from response
				var rawAddresses = AddressesRegex().Matches(xmlData);

				var addresses = new List<Address>();
				foreach (Match rawAddress in rawAddresses)
				{
					var address = new Address
					{
						Property = rawAddress.Groups["address"].Value.Trim(),
						Uid = rawAddress.Groups["uprn"].Value,
						Postcode = postcode,
					};
					addresses.Add(address);
				}

				return new GetAddressesResponse
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
				var requestUrl = $"https://eastcambs-self.achieveservice.com/appshost/firmstep/self/apps/custompage/bincollections?uprn={address.Uid}";

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = requestUrl,
					Method = "GET",
					Headers = new Dictionary<string, string> {
						{ "User-Agent", Constants.UserAgent }
					},
				};

				return new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest
				};
			}
			// Process bin days from response
			else if (clientSideResponse.RequestId == 1)
			{
				// Get bin days from response
				var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content);

				var binDays = new List<BinDay>();
				foreach (Match rawBinDay in rawBinDays)
				{
					var binTypeStr = rawBinDay.Groups["binType"].Value.Trim();
					var dateStr = WhitespaceRegex().Replace(rawBinDay.Groups["date"].Value.Trim(), " ");

					// Parse date string (e.g. "Fri - 26 Sep 2025")
					var date = DateOnly.ParseExact(
						dateStr,
						"ddd - dd MMM yyyy",
						CultureInfo.InvariantCulture
					);

					// Get matching bin types from the bin ID using the keys
					var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, binTypeStr);

					var binDay = new BinDay
					{
						Date = date,
						Address = address,
						Bins = matchedBinTypes,
					};

					binDays.Add(binDay);
				}

				return new GetBinDaysResponse
				{
					BinDays = ProcessingUtilities.ProcessBinDays(binDays),
				};
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}
	}
}
