namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Blackpool Borough Council.
/// </summary>
internal sealed partial class BlackpoolBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Blackpool Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.blackpool.gov.uk/Residents/Waste-and-recycling/Bin-collections/Bin-collections.aspx");

	/// <inheritdoc/>
	public override string GovUkId => "blackpool";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Grey,
			Keys = [ "Grey lid bin" ],
		},
		new()
		{
			Name = "Dry Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Blue lid bin" ],
		},
		new()
		{
			Name = "Paper and Cardboard Recycling",
			Colour = BinColour.Brown,
			Keys = [ "Brown lid bin" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Green lid bin" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "Food caddy" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// The bins calendar URL used to establish a session.
	/// </summary>
	private const string _binsCalendarUrl = "https://selfservice.blackpool.gov.uk/ss/ssforms/binsCalendar";

	/// <summary>
	/// Regex for extracting the collection-data JSON payload from the HTML.
	/// </summary>
	[GeneratedRegex(@"<div id=""collection-data""[^>]*>(?<collectionData>.*?)</div>", RegexOptions.Singleline)]
	private static partial Regex CollectionDataRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for loading the bins calendar page
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _binsCalendarUrl,
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for getting addresses
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "inputtedAddress", postcode },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://selfservice.blackpool.gov.uk/SS/SSForms/binsCalendar/GetAddressOptions",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
					{ "x-requested-with", Constants.XmlHttpRequest },
					{ "cookie", requestCookies },
				},
				Body = requestBody,
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 2)
		{
			using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in jsonDoc.RootElement.EnumerateArray())
			{
				var property = rawAddress.GetProperty("Address").GetString()!.Trim();
				var uprn = rawAddress.GetProperty("UPRN").GetString()!.Trim();

				// Uid format: "uprn;addressName"
				var address = new Address
				{
					Property = property,
					Postcode = postcode,
					Uid = $"{uprn};{property}",
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
		// Prepare client-side request for loading the bins calendar page
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _binsCalendarUrl,
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting collection data
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			// Uid format: "uprn;addressName"
			var uidParts = address.Uid!.Split(';', 2);
			var uprn = uidParts[0];
			var addressName = uidParts[1];

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "UPRN", uprn },
				{ "addressName", addressName },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://selfservice.blackpool.gov.uk/SS/SSForms/binsCalendar/ShowCalendarData",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
					{ "x-requested-with", Constants.XmlHttpRequest },
					{ "cookie", requestCookies },
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
		else if (clientSideResponse.RequestId == 2)
		{
			var collectionData = CollectionDataRegex().Match(clientSideResponse.Content).Groups["collectionData"].Value;

			using var jsonDoc = JsonDocument.Parse(collectionData);

			// Iterate through each collection entry, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (var rawCollection in jsonDoc.RootElement.EnumerateArray())
			{
				var service = rawCollection.GetProperty("FeatureName").GetString()!.Trim();
				var collectionDate = rawCollection.GetProperty("ScheduledDate").GetString()!.Trim();

				var date = DateUtilities.ParseDateExact(collectionDate, "yyyy-MM-dd");
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
}
