namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Sunderland City Council.
/// </summary>
internal sealed partial class SunderlandCityCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Sunderland City Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.sunderland.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "sunderland";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "Household Waste" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling Waste" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden Waste" ],
		},
	];

	/// <summary>
	/// Regex for the viewstate token values from input fields.
	/// </summary>
	[GeneratedRegex(@"<input[^>]*?(?:name|id)=[\""]__VIEWSTATE[\""][^>]*?value=[\""](?<viewStateValue>[^\""]*)[\""][^>]*?/?>")]
	private static partial Regex ViewStateTokenRegex();

	/// <summary>
	/// Regex for the viewstate generator values from input fields.
	/// </summary>
	[GeneratedRegex(@"<input[^>]*?(?:name|id)=[\""]__VIEWSTATEGENERATOR[\""][^>]*?value=[\""](?<viewStateGenerator>[^\""]*)[\""][^>]*?/?>")]
	private static partial Regex ViewStateGeneratorRegex();

	/// <summary>
	/// Regex for the event validation values from input fields.
	/// </summary>
	[GeneratedRegex(@"<input[^>]*?(?:name|id)=[\""]__EVENTVALIDATION[\""][^>]*?value=[\""](?<eventValidationValue>[^\""]*)[\""][^>]*?/?>")]
	private static partial Regex EventValidationRegex();

	/// <summary>
	/// Regex for the addresses from the options elements.
	/// </summary>
	[GeneratedRegex(@"<option value=""(?<uid>[^""]+)"">(?<address>[^<]+)</option>")]
	private static partial Regex AddressesRegex();

	/// <summary>
	/// Regex for the bin days from the data elements.
	/// </summary>
	[GeneratedRegex(@"<h2[^>]*>(?<service>[^<]+)</h2>.*?class=""datelabel"">\s*(?<date>[^<]+)\s*</span>", RegexOptions.Singleline)]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting token
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://webapps.sunderland.gov.uk/WEBAPPS/WSS/Sunderland_Portal/Forms/bindaychecker.aspx?ccp=true",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
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
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var viewState = ViewStateTokenRegex().Match(clientSideResponse.Content).Groups["viewStateValue"].Value;
			var viewStateGenerator = ViewStateGeneratorRegex().Match(clientSideResponse.Content).Groups["viewStateGenerator"].Value;
			var eventValidation = EventValidationRegex().Match(clientSideResponse.Content).Groups["eventValidationValue"].Value;

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "__VIEWSTATE", viewState },
				{ "__VIEWSTATEGENERATOR", viewStateGenerator },
				{ "__EVENTVALIDATION", eventValidation },
				{ "ctl00$ContentPlaceHolder1$tbPostCode$controltext", postcode },
				{ "ctl00$ContentPlaceHolder1$btnLLPG", "Find Address" },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://webapps.sunderland.gov.uk/WEBAPPS/WSS/Sunderland_Portal/Forms/bindaychecker.aspx?ccp=true",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", "application/x-www-form-urlencoded" },
					{ "cookie", cookie },
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
			var rawAddresses = AddressesRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var uid = rawAddress.Groups["uid"].Value;

				if (uid == "0")
				{
					continue;
				}

				var address = new Address
				{
					Property = rawAddress.Groups["address"].Value.Trim(),
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

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting token
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://webapps.sunderland.gov.uk/WEBAPPS/WSS/Sunderland_Portal/Forms/bindaychecker.aspx?ccp=true",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting addresses
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var viewState = ViewStateTokenRegex().Match(clientSideResponse.Content).Groups["viewStateValue"].Value;
			var viewStateGenerator = ViewStateGeneratorRegex().Match(clientSideResponse.Content).Groups["viewStateGenerator"].Value;
			var eventValidation = EventValidationRegex().Match(clientSideResponse.Content).Groups["eventValidationValue"].Value;

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "__VIEWSTATE", viewState },
				{ "__VIEWSTATEGENERATOR", viewStateGenerator },
				{ "__EVENTVALIDATION", eventValidation },
				{ "ctl00$ContentPlaceHolder1$tbPostCode$controltext", address.Postcode! },
				{ "ctl00$ContentPlaceHolder1$btnLLPG", "Find Address" },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://webapps.sunderland.gov.uk/WEBAPPS/WSS/Sunderland_Portal/Forms/bindaychecker.aspx?ccp=true",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", "application/x-www-form-urlencoded" },
					{ "cookie", cookie },
				},
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata = { { "cookie", cookie } },
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting bin days
		else if (clientSideResponse.RequestId == 2)
		{
			var viewState = ViewStateTokenRegex().Match(clientSideResponse.Content).Groups["viewStateValue"].Value;
			var viewStateGenerator = ViewStateGeneratorRegex().Match(clientSideResponse.Content).Groups["viewStateGenerator"].Value;
			var eventValidation = EventValidationRegex().Match(clientSideResponse.Content).Groups["eventValidationValue"].Value;

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "__EVENTTARGET", "ctl00$ContentPlaceHolder1$ddlAddresses" },
				{ "__VIEWSTATE", viewState },
				{ "__VIEWSTATEGENERATOR", viewStateGenerator },
				{ "__EVENTVALIDATION", eventValidation },
				{ "ctl00$ContentPlaceHolder1$tbPostCode$controltext", address.Postcode! },
				{ "ctl00$ContentPlaceHolder1$ddlAddresses", address.Uid! },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = "https://webapps.sunderland.gov.uk/WEBAPPS/WSS/Sunderland_Portal/Forms/bindaychecker.aspx?ccp=true",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", "application/x-www-form-urlencoded" },
					{ "cookie", clientSideResponse.Options.Metadata["cookie"] },
				},
				Body = requestBody,
				Options = clientSideResponse.Options,
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
				var date = rawBinDay.Groups["date"].Value.Trim();

				var collectionDate = DateOnly.ParseExact(
					date,
					"dddd d MMMM yyyy",
					CultureInfo.InvariantCulture,
					DateTimeStyles.None
				);

				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				var binDay = new BinDay
				{
					Date = collectionDate,
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

		throw new InvalidOperationException("Invalid client-side request.");
	}
}
