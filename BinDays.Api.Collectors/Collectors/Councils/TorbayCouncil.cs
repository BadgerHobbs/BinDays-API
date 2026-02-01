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
/// Collector implementation for Torbay Council.
/// </summary>
internal sealed partial class TorbayCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Torbay Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.torbay.gov.uk/recycling/bin-collections/");

	/// <inheritdoc/>
	public override string GovUkId => "torbay";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Grey,
			Type = BinType.Bin,
			Keys = [ "Domestic", "General", "Refuse" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Green,
			Type = BinType.Box,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Type = BinType.Bin,
			Keys = [ "Garden" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Brown,
			Type = BinType.Caddy,
			Keys = [ "Food" ],
		},
	];

	/// <summary>
	/// Regex for the __RequestVerificationToken.
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
	/// Regex for bin day rows.
	/// </summary>
	[GeneratedRegex(@"resirow[^>]*>\s*<div[^>]*class=""col[^""]*""[^>]*>.*?<div[^>]*class=""col""[^>]*>(?<date>[^<]+)</div>\s*<div[^>]*class=""col""[^>]*>(?<service>[^<]+)</div", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
	private static partial Regex BinDayRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting form tokens and cookies
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://selfservice-torbay.servicebuilder.co.uk/renderform?t=62&k=09B72FF904A21A4B01A72AB6CCF28DC95105031C",
				Method = "GET",
				Headers = new()
				{
					{ "User-Agent", Constants.UserAgent },
				},
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
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "query", postcode },
				{ "searchNlpg", "False" },
				{ "classification", string.Empty },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://selfservice-torbay.servicebuilder.co.uk/core/addresslookup",
				Method = "POST",
				Headers = new()
				{
					{ "User-Agent", Constants.UserAgent },
					{ "Content-Type", "application/x-www-form-urlencoded" },
					{ "Cookie", requestCookies },
					{ "x-requested-with", "XMLHttpRequest" },
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
			using var document = JsonDocument.Parse(clientSideResponse.Content);

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var element in document.RootElement.EnumerateArray())
			{
				var uid = element.GetProperty("Key").GetString();
				var addressText = element.GetProperty("Value").GetString();

				var address = new Address
				{
					Property = addressText?.Trim(),
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
		// Prepare client-side request for getting form tokens and cookies
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://selfservice-torbay.servicebuilder.co.uk/renderform?t=62&k=09B72FF904A21A4B01A72AB6CCF28DC95105031C",
				Method = "GET",
				Headers = new()
				{
					{ "User-Agent", Constants.UserAgent },
				},
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
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);

			var token = RequestVerificationTokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;
			var formGuid = FormGuidRegex().Match(clientSideResponse.Content).Groups["formGuid"].Value;
			var objectTemplateId = ObjectTemplateIdRegex().Match(clientSideResponse.Content).Groups["objectTemplateId"].Value;

			if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(formGuid) || string.IsNullOrWhiteSpace(objectTemplateId))
			{
				throw new InvalidOperationException("Failed to extract one or more form tokens from the page. The council website may have changed.");
			}

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "__RequestVerificationToken", token },
				{ "FormGuid", formGuid },
				{ "ObjectTemplateID", objectTemplateId },
				{ "Trigger", "submit" },
				{ "CurrentSectionID", "0" },
				{ "TriggerCtl", string.Empty },
				{ "FF1168", address.Uid! },
				{ "FF1168lbltxt", "Please select your address" },
				{ "FF1168-text", address.Postcode! },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://selfservice-torbay.servicebuilder.co.uk/renderform/Form",
				Method = "POST",
				Headers = new()
				{
					{ "User-Agent", Constants.UserAgent },
					{ "Content-Type", "application/x-www-form-urlencoded" },
					{ "Cookie", requestCookies },
					{ "x-requested-with", "XMLHttpRequest" },
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
			// Iterate through each bin day row, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match match in BinDayRegex().Matches(clientSideResponse.Content)!)
			{
				var dateString = match.Groups["date"].Value.Trim();
				var service = match.Groups["service"].Value.Trim();

				var date = DateOnly.ParseExact(
					dateString,
					"dddd dd MMMM yyyy",
					CultureInfo.InvariantCulture,
					DateTimeStyles.None
				);

				var bins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = bins,
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
