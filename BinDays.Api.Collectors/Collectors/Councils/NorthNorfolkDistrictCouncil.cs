namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for North Norfolk District Council.
/// </summary>
internal sealed partial class NorthNorfolkDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "North Norfolk District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.north-norfolk.gov.uk/tasks/environmental-services/view-bin-collections-days/");

	/// <inheritdoc/>
	public override string GovUkId => "north-norfolk";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Grey,
			Keys = [ "Grey bin" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Green,
			Keys = [ "Green bin" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Brown bin" ],
		},
	];

	/// <summary>
	/// The base URL of the council's forms platform.
	/// </summary>
	private const string _baseUrl = "https://forms.north-norfolk.gov.uk/xforms";

	/// <summary>
	/// Regex for the request verification token from the address page.
	/// </summary>
	[GeneratedRegex(@"name=""__RequestVerificationToken"" type=""hidden"" value=""(?<token>[^""]+)""")]
	private static partial Regex TokenRegex();

	/// <summary>
	/// Regex for the bin names and next collection dates from the bin collection details page.
	/// </summary>
	[GeneratedRegex(@"Your next (?<service>[^<]+?) collection is <strong>(?<date>[^<]+)</strong>")]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/AddressSearch/GetAddressList?postcode={postcode}&localSearch=True",
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var addressElement in jsonDoc.RootElement.EnumerateArray())
			{
				var uid = addressElement.GetProperty("value").GetString()!;

				// Skip the "Please select an address..." placeholder option
				if (uid == "0")
				{
					continue;
				}

				var address = new Address
				{
					Property = addressElement.GetProperty("text").GetString()!.Trim(),
					Postcode = postcode,
					Uid = uid,
				};

				addresses.Add(address);
			}

			var getAddressesResponse = new GetAddressesResponse
			{
				Addresses = [.. addresses],
			};

			return getAddressesResponse;
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for starting a new bin days journey, skipping the journey landing page
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseUrl}/Launch/DoNew/BinDaysJourney",
				Method = "GET",
				Options = new ClientSideOptions
				{
					FollowRedirects = false,
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting the address page
		else if (clientSideResponse.RequestId == 1)
		{
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseUrl}/Address/Show/CollectionAddress",
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
		// Prepare client-side request for selecting the address, which redirects to the bin collection details page
		else if (clientSideResponse.RequestId == 2)
		{
			var token = TokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;

			var antiforgeryCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);
			var cookies = $"{clientSideResponse.Options.Metadata["cookies"]}; {antiforgeryCookies}";

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "GisUprn", address.Uid! },
				{ "__RequestVerificationToken", token },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"{_baseUrl}/Address/Show/CollectionAddress",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
					{ "cookie", cookies },
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
		else if (clientSideResponse.RequestId == 3)
		{
			var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["service"].Value.Trim();
				var collectionDate = rawBinDay.Groups["date"].Value.Trim();

				// Parse date string (e.g. 'Monday 19 October')
				var date = DateUtilities.ParseDateInferringYear(collectionDate, "dddd d MMMM");

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = matchedBinTypes,
				};

				binDays.Add(binDay);
			}

			var getBinDaysResponse = new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};

			return getBinDaysResponse;
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}
}
