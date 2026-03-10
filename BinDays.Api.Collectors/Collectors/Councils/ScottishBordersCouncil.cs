namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Scottish Borders Council.
/// </summary>
internal sealed partial class ScottishBordersCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Scottish Borders Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.scotborders.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "scottish-borders";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "General Waste" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling" ],
		},
	];

	/// <summary>
	/// The base URL for the collection calendar.
	/// </summary>
	private const string CollectionCalendarUrl = "https://scotborders-live-portal.bartecmunicipal.com/Embeddable/CollectionCalendar";

	/// <summary>
	/// Regex for extracting the request verification token.
	/// </summary>
	[GeneratedRegex(@"name=""__RequestVerificationToken"" type=""hidden"" value=""(?<token>[^""]+)""")]
	private static partial Regex TokenRegex();

	/// <summary>
	/// Regex for capturing data sources embedded in the page scripts.
	/// </summary>
	[GeneratedRegex(@"dataSource"": ejs\.data\.DataUtil\.parse\.isJson\((?<json>\[.*?\])\)", RegexOptions.Singleline)]
	private static partial Regex DataSourceRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting the initial page and token
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = CollectionCalendarUrl,
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for searching the postcode
		else if (clientSideResponse.RequestId == 1)
		{
			var token = TokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;

			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "SelectedPostcode", postcode },
				{ "__RequestVerificationToken", token },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{CollectionCalendarUrl}?handler=SearchPostcode",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
					{ "cookie", cookies },
					{ "origin", CollectionCalendarUrl },
					{ "referer", CollectionCalendarUrl },
				},
				Body = requestBody,
				Options = new ClientSideOptions
				{
					FollowRedirects = false,
					Metadata =
					{
						{ "cookies", cookies },
					},
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for retrieving addresses
		else if (clientSideResponse.RequestId == 2)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var postcodeCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);
			var cookies = CombineCookies(clientSideResponse.Options.Metadata["cookies"], postcodeCookies);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = CollectionCalendarUrl,
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "cookie", cookies },
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 3)
		{
			var addressData = DataSourceRegex().Match(clientSideResponse.Content).Groups["json"].Value;

			using var document = JsonDocument.Parse(addressData);

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var addressElement in document.RootElement.EnumerateArray())
			{
				var property = addressElement.GetProperty("Premises").GetString()!;
				var uprn = addressElement.GetProperty("UPRN").GetDouble().ToString("0", CultureInfo.InvariantCulture);

				var address = new Address
				{
					Property = property.Trim(),
					Postcode = postcode,
					Uid = uprn,
				};

				addresses.Add(address);
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
		// Prepare client-side request for getting the initial page and token
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = CollectionCalendarUrl,
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for searching the postcode
		else if (clientSideResponse.RequestId == 1)
		{
			var token = TokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;

			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "SelectedPostcode", address.Postcode! },
				{ "__RequestVerificationToken", token },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{CollectionCalendarUrl}?handler=SearchPostcode",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
					{ "cookie", cookies },
					{ "origin", CollectionCalendarUrl },
					{ "referer", CollectionCalendarUrl },
				},
				Body = requestBody,
				Options = new ClientSideOptions
				{
					FollowRedirects = false,
					Metadata =
					{
						{ "cookies", cookies },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for loading the address page
		else if (clientSideResponse.RequestId == 2)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var postcodeCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);
			var cookies = CombineCookies(clientSideResponse.Options.Metadata["cookies"], postcodeCookies);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = CollectionCalendarUrl,
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "cookie", cookies },
				},
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookies", cookies },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for selecting the premises
		else if (clientSideResponse.RequestId == 3)
		{
			var token = TokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;

			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var pageCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);
			var cookies = CombineCookies(clientSideResponse.Options.Metadata["cookies"], pageCookies);

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "SelectedPostcode", address.Postcode! },
				{ "SelectedPremises", address.Uid! },
				{ "__RequestVerificationToken", token },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 4,
				Url = $"{CollectionCalendarUrl}?handler=SelectPrem",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
					{ "cookie", cookies },
					{ "origin", CollectionCalendarUrl },
					{ "referer", CollectionCalendarUrl },
				},
				Body = requestBody,
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 4)
		{
			var dataSources = DataSourceRegex().Matches(clientSideResponse.Content)!;
			var binData = dataSources[1].Groups["json"].Value;

			using var document = JsonDocument.Parse(binData);

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var binDayElement in document.RootElement.EnumerateArray())
			{
				var service = binDayElement.GetProperty("Subject").GetString()!;
				var collectionDate = binDayElement.GetProperty("StartTime").GetString()!;

				var date = DateUtilities.ParseDateExact(collectionDate, "yyyy-MM-ddTHH:mm:ss");

				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				var binDay = new BinDay
				{
					Date = date,
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
	/// Combines multiple cookie strings, removing any empty entries.
	/// </summary>
	private static string CombineCookies(params string[] cookieParts)
	{
		return string.Join("; ", cookieParts.Where(cookie => !string.IsNullOrWhiteSpace(cookie)));
	}
}
