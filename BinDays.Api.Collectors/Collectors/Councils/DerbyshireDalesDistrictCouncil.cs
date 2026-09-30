namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Derbyshire Dales District Council.
/// </summary>
internal sealed partial class DerbyshireDalesDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Derbyshire Dales District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.derbyshiredales.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "derbyshire-dales";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Grey,
			Keys = [ "Domestic Waste 140L Bin", "Domestic Waste 240L Waste Bin" ],
		},
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Domestic Waste Sacks" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Food Waste 23L Caddy" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling 240L Waste Bin" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling Blue Waste Box" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Paper and Card Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Paper Waste Sacks" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Plastic Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling Waste Sacks" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Garden Waste 240L Bin" ],
		},
	];

	/// <summary>
	/// The URL for the bin collections form.
	/// </summary>
	private const string _formUrl = "https://selfserve.derbyshiredales.gov.uk/renderform?t=103&k=9644C066D2168A4C21BCDA351DA2642526359DFF";

	/// <summary>
	/// Regex for the __RequestVerificationToken value.
	/// </summary>
	[GeneratedRegex(@"<input[^>]*name=""__RequestVerificationToken""[^>]*value=""(?<token>[^""]+)""", RegexOptions.IgnoreCase)]
	private static partial Regex RequestVerificationTokenRegex();

	/// <summary>
	/// Regex for the FormGuid value.
	/// </summary>
	[GeneratedRegex(@"<input[^>]*name=""FormGuid""[^>]*value=""(?<formGuid>[^""]+)""", RegexOptions.IgnoreCase)]
	private static partial Regex FormGuidRegex();

	/// <summary>
	/// Regex for the ObjectTemplateID value.
	/// </summary>
	[GeneratedRegex(@"<input[^>]*name=""ObjectTemplateID""[^>]*value=""(?<objectTemplateId>[^""]+)""", RegexOptions.IgnoreCase)]
	private static partial Regex ObjectTemplateIdRegex();

	/// <summary>
	/// Regex for bin collection rows in the submitted form response.
	/// </summary>
	[GeneratedRegex(@"<div class=""col-sm-5"">\s*<strong>[^<]+<\/strong>\s*(?<date>[^<]+)<\/div>\s*<div class=""col-sm-6"">\s*<strong>(?<service>[^<]+)<\/strong>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
	private static partial Regex BinDayRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			Dictionary<string, string> requestFormData = new()
			{
				{ "query", postcode },
			};

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://selfserve.derbyshiredales.gov.uk/core/addresslookup",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(requestFormData),
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
			using var document = JsonDocument.Parse(clientSideResponse.Content);

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var rawAddress in document.RootElement.EnumerateObject())
			{
				var address = new Address
				{
					Property = WebUtility.HtmlDecode(rawAddress.Value.GetString()!).Trim(),
					Postcode = postcode,
					Uid = rawAddress.Name,
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
		// Prepare client-side request for getting form cookies and tokens
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _formUrl,
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting bin days
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var requestVerificationToken = RequestVerificationTokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;
			var formGuid = FormGuidRegex().Match(clientSideResponse.Content).Groups["formGuid"].Value;
			var objectTemplateId = ObjectTemplateIdRegex().Match(clientSideResponse.Content).Groups["objectTemplateId"].Value;

			Dictionary<string, string> requestFormData = new()
			{
				{ "__RequestVerificationToken", requestVerificationToken },
				{ "FormGuid", formGuid },
				{ "ObjectTemplateID", objectTemplateId },
				{ "Trigger", "submit" },
				{ "CurrentSectionID", "0" },
				{ "FF2924", address.Uid! },
			};

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://selfserve.derbyshiredales.gov.uk/renderform/Form",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
					{ "cookie", requestCookies },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(requestFormData),
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
			var rawBinDays = BinDayRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin day row, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var collectionDate = WebUtility.HtmlDecode(rawBinDay.Groups["date"].Value).Trim();
				var service = WebUtility.HtmlDecode(rawBinDay.Groups["service"].Value).Trim();

				var date = DateUtilities.ParseDateExact(collectionDate, "dd MMMM, yyyy");
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

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}
}
