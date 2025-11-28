namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors.Vendors;
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Globalization;
	using System.Text.Json;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Collector implementation for New Forest District Council.
	/// </summary>
	internal sealed partial class NewForestDistrictCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "New Forest District Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.newforest.gov.uk/bin-collection-days");

		/// <inheritdoc/>
		public override string GovUkId => "new-forest";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Rubbish",
				Colour = BinColour.Black,
				Keys = new List<string>() { "General" }.AsReadOnly(),
			},
			new()
			{
				Name = "Recycling",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Recycl" }.AsReadOnly(),
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Brown,
				Keys = new List<string>() { "Garden" }.AsReadOnly(),
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
				Name = "Glass Recycling",
				Colour = BinColour.Black,
				Keys = new List<string>() { "Glass" }.AsReadOnly(),
				Type = BinType.Box,
			},
		}.AsReadOnly();

		/// <summary>
		/// Regex for the session token value from an input field.
		/// </summary>
		[GeneratedRegex(@"FIND_MY_BIN_BAR.eb\?ebz=([^'""]+)")]
		private static partial Regex TokenRegex();

		/// <summary>
		/// Regex for the addresses from the options elements.
		/// </summary>
		[GeneratedRegex(@"<option\s+value=""(?<uid>\d+)""[^>]*>\s*(?<address>.*?)\s*</option>")]
		private static partial Regex AddressRegex();

		/// <summary>
		/// Regex for the bin days from the data table elements.
		/// </summary>
		[GeneratedRegex(@"<td[^>]+>\s*<div[^>]+>\s*<div[^>]+>\s*(?<date>[^<]+).+?<td[^>]+>\s*<div[^>]+>\s*<div[^>]+>\s*(?<service>[^<]+)")]
		private static partial Regex BinDaysRegex();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting the cookie
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = "https://forms.newforest.gov.uk/ufs/ufsmain?formid=FIND_MY_BIN_BAR",
					Method = "GET",
					Headers = new Dictionary<string, string>() {
						{"user-agent", Constants.UserAgent},
					},
					Options = new ClientSideOptions
					{
						FollowRedirects = false,
					},
				};

				var getAddressesResponse = new GetAddressesResponse()
				{
					NextClientSideRequest = clientSideRequest
				};

				return getAddressesResponse;
			}
			// Prepare client-side request for getting the token
			else if (clientSideResponse.RequestId == 1)
			{
				var cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
					clientSideResponse.Headers["set-cookie"]);
				// Prepare client-side request
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = "https://forms.newforest.gov.uk/ufs/ufsmain?formid=FIND_MY_BIN_BAR",
					Method = "GET",
					Headers = new Dictionary<string, string>() {
						{"user-agent", Constants.UserAgent},
						{"cookie", cookie},
					},
					Options = new ClientSideOptions
					{
						Metadata = {
							{ "cookie", cookie },
						}
					},
				};

				var getAddressesResponse = new GetAddressesResponse()
				{
					NextClientSideRequest = clientSideRequest
				};

				return getAddressesResponse;
			}
			// Prepare client-side request for getting addresses
			else if (clientSideResponse.RequestId == 2)
			{
				var token = TokenRegex().Match(clientSideResponse.Content).Groups[1].Value;

				// Prepare client-side request
				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
				{
					{"formid", "/Forms/FIND_MY_BIN_BAR"},
					{"ebs", token},
					{"origrequrl", "https://forms.newforest.gov.uk/ufs/FIND_MY_BIN_BAR.eb"},
					{"formstack", "FIND_MY_BIN_BAR:71bc4a7b-7a80-40a9-848c-87ab447b90ae"},
					{"PAGE:F", "CTID-EBTnjgwK-_"},
					{"CTRL:JmLqCKl2:_:A", postcode},
					{"HID:inputs", "ICTRL:JmLqCKl2:_:A,ACTRL:JmLqCKl2:_:B.h,ACTRL:EBTnjgwK:_,APAGE:E.h,APAGE:B.h,APAGE:N.h,APAGE:S.h,APAGE:R.h"},
					{"CTRL:EBTnjgwK:_", "Submit"}
				});

				var requestHeaders = new Dictionary<string, string>() {
					{"user-agent", Constants.UserAgent},
					{"content-type", "application/x-www-form-urlencoded"},
					{"cookie", clientSideResponse.Options.Metadata["cookie"]},
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 3,
					Url = "https://forms.newforest.gov.uk/ufs/ufsajax?ebz=" + token,
					Method = "POST",
					Headers = requestHeaders,
					Body = requestBody,
				};

				var getAddressesResponse = new GetAddressesResponse()
				{
					NextClientSideRequest = clientSideRequest
				};

				return getAddressesResponse;
			}
			// Process addresses from response
			else if (clientSideResponse.RequestId == 3)
			{
				// Parse the JSON response
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

				// Find the first html key that contains <option
				string? htmlWithOptions = null;
				if (jsonDoc.RootElement.TryGetProperty("updatedControls", out var updatedControls))
				{
					foreach (var control in updatedControls.EnumerateArray())
					{
						if (control.TryGetProperty("html", out var htmlProperty) &&
							htmlProperty.ValueKind == JsonValueKind.String &&
							htmlProperty.GetString()!.Contains("<option") == true)
						{
							htmlWithOptions = htmlProperty.GetString()!.Replace("\\\"", "\"");
							break;
						}
					}
				}

				if (htmlWithOptions == null)
				{
					throw new InvalidOperationException("Could not find bin days HTML");
				}

				// Get addresses from response
				var rawAddresses = AddressRegex().Matches(htmlWithOptions)!;

				// Iterate through each address, and create a new address object
				var addresses = new List<Address>();
				foreach (Match rawAddress in rawAddresses)
				{
					var uid = rawAddress.Groups["uid"].Value;

					// Exclude placeholder/invalid options
					if (uid == "-1" || uid == "111111")
					{
						continue;
					}

					var address = new Address()
					{
						Property = rawAddress.Groups["address"].Value.Replace("&nbsp", " "),
						Postcode = postcode,
						Uid = uid,
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
			// Prepare client-side request for getting the cookie
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = "https://forms.newforest.gov.uk/ufs/ufsmain?formid=FIND_MY_BIN_BAR",
					Method = "GET",
					Headers = new Dictionary<string, string>() {
						{"user-agent", Constants.UserAgent},
					},
					Options = new ClientSideOptions
					{
						FollowRedirects = false,
					},
				};

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Prepare client-side request for getting the token
			else if (clientSideResponse.RequestId == 1)
			{
				var cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
					clientSideResponse.Headers["set-cookie"]);
				// Prepare client-side request
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = "https://forms.newforest.gov.uk/ufs/ufsmain?formid=FIND_MY_BIN_BAR",
					Method = "GET",
					Headers = new Dictionary<string, string>() {
						{"user-agent", Constants.UserAgent},
						{"cookie", cookie},
					},
					Options = new ClientSideOptions
					{
						Metadata = {
							{ "cookie", cookie },
						}
					},
				};

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Prepare client-side request for getting addresses
			else if (clientSideResponse.RequestId == 2)
			{
				var token = TokenRegex().Match(clientSideResponse.Content).Groups[1].Value;

				// Prepare client-side request
				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
				{
					{"formid", "/Forms/FIND_MY_BIN_BAR"},
					{"ebs", token},
					{"origrequrl", "https://forms.newforest.gov.uk/ufs/FIND_MY_BIN_BAR.eb"},
					{"formstack", "FIND_MY_BIN_BAR:71bc4a7b-7a80-40a9-848c-87ab447b90ae"},
					{"CTRL:KOeKcmrC:_:A", address.Uid!},
					{"HID:inputs", "ICTRL:KOeKcmrC:_:A,ACTRL:KOeKcmrC:_:B.h,ACTRL:QxB4NyYs:_,ACTRL:Ggx8Z7ze:_,APAGE:E.h,APAGE:B.h,APAGE:N.h,APAGE:S.h,APAGE:R.h"},
					{"CTRL:QxB4NyYs:_", "Submit"},
				});

				var requestHeaders = new Dictionary<string, string>() {
					{"user-agent", Constants.UserAgent},
					{"content-type", "application/x-www-form-urlencoded"},
					{"cookie", clientSideResponse.Options.Metadata["cookie"]},
				};

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 3,
					Url = "https://forms.newforest.gov.uk/ufs/ufsajax?ebz=" + token,
					Method = "POST",
					Headers = requestHeaders,
					Body = requestBody,
				};

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					NextClientSideRequest = clientSideRequest
				};

				return getBinDaysResponse;
			}
			// Process bin days from response
			else if (clientSideResponse.RequestId == 3)
			{
				// Parse the JSON response
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

				// Find the first html key that contains <option
				string? htmlWithOptions = null;
				if (jsonDoc.RootElement.TryGetProperty("updatedControls", out var updatedControls))
				{
					foreach (var control in updatedControls.EnumerateArray())
					{
						if (control.TryGetProperty("html", out var htmlProperty) &&
							htmlProperty.ValueKind == JsonValueKind.String &&
							htmlProperty.GetString()?.Contains(">Future collections<") == true)
						{
							htmlWithOptions = htmlProperty.GetString()!.Replace("\\\"", "\"");
							break;
						}
					}
				}

				if (htmlWithOptions == null)
				{
					throw new InvalidOperationException("Could not find bin days HTML");
				}

				htmlWithOptions = htmlWithOptions.Replace("\r\n", String.Empty);

				var rawBinDays = BinDaysRegex().Matches(htmlWithOptions)!;

				// Example: Tuesday August 10, 2021
				var format = "dddd MMMM d, yyyy";

				var binDays = new List<BinDay>();
				foreach (Match rawBinDay in rawBinDays)
				{
					var service = rawBinDay.Groups["service"].Value;
					var collectionDate = rawBinDay.Groups["date"].Value;

					var date = DateOnly.ParseExact(collectionDate, format, CultureInfo.InvariantCulture);
					var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

					var binDay = new BinDay()
					{
						Date = date,
						Address = address,
						Bins = matchedBinTypes,
					};

					binDays.Add(binDay);
				}

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = ProcessingUtilities.ProcessBinDays(binDays),
				};

				return getBinDaysResponse;
			}

			throw new InvalidOperationException("Invalid client-side request.");
		}
	}
}
