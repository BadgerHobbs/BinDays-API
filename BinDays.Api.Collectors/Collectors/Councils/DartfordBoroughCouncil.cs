namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Dartford Borough Council.
/// </summary>
internal sealed partial class DartfordBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Dartford Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.dartford.gov.uk/waste-recycling/collection-day");

	/// <inheritdoc/>
	public override string GovUkId => "dartford";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "Refuse" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Grey,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Black,
			Keys = [ "Food Waste" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// The URL of the council's bin collections form.
	/// </summary>
	private const string _formUrl = "https://windmz.dartford.gov.uk/ufs/WS_CHECK_COLLECTIONS.eb";

	/// <summary>
	/// Regex for the form session token from the redirect location.
	/// </summary>
	[GeneratedRegex(@"ebz=(?<token>[^&]+)")]
	private static partial Regex TokenRegex();

	/// <summary>
	/// Regex for the addresses from the address select options.
	/// </summary>
	[GeneratedRegex(@"<option[^>]*value=""(?<address>[^""]+)""")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the bin days from the collections table rows.
	/// </summary>
	[GeneratedRegex(@"data-eb-colheader=""Collection Type""[^>]*>\s*<div[^>]*>\s*<div[^>]*>(?<service>[^<]+)</div>.*?data-eb-colheader=""Date""[^>]*>\s*<div[^>]*>\s*<div[^>]*>(?<date>[^<]+)</div>", RegexOptions.Singleline)]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for starting a form session
		if (clientSideResponse == null)
		{
			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = CreateSessionRequest(),
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for searching the postcode
		else if (clientSideResponse.RequestId == 1)
		{
			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = CreatePostcodeRequest(clientSideResponse, postcode),
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 2)
		{
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				// The form only accepts the option value exactly as listed, including its trailing space
				var uid = WebUtility.HtmlDecode(rawAddress.Groups["address"].Value);

				var address = new Address
				{
					Property = uid.Trim(),
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
		// Prepare client-side request for starting a form session
		if (clientSideResponse == null)
		{
			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = CreateSessionRequest(),
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for searching the postcode, which loads the address list into the session
		else if (clientSideResponse.RequestId == 1)
		{
			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = CreatePostcodeRequest(clientSideResponse, address.Postcode!),
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for selecting the address
		else if (clientSideResponse.RequestId == 2)
		{
			var token = clientSideResponse.Options.Metadata["token"];
			var cookie = clientSideResponse.Options.Metadata["cookie"];

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "CTRL:5IqPlilK:_:A", address.Uid! },
				{ "CTRL:5IqPlilK:_:B.h", "X" },
				{ "HID:inputs", "ICTRL:5IqPlilK:_:A" },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"{_formUrl}?ebz={token}",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
					{ "cookie", cookie },
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

				var date = DateUtilities.ParseDateExact(collectionDate, "dd/MM/yyyy");

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

	/// <summary>
	/// Creates the client-side request for starting a form session, which redirects with the session token.
	/// </summary>
	private static ClientSideRequest CreateSessionRequest()
	{
		var clientSideRequest = new ClientSideRequest
		{
			RequestId = 1,
			Url = _formUrl,
			Method = "GET",
			Options = new ClientSideOptions
			{
				FollowRedirects = false,
			},
		};

		return clientSideRequest;
	}

	/// <summary>
	/// Creates the client-side request for searching a postcode within the form session.
	/// </summary>
	private static ClientSideRequest CreatePostcodeRequest(ClientSideResponse clientSideResponse, string postcode)
	{
		var token = TokenRegex().Match(clientSideResponse.Headers["location"]).Groups["token"].Value;
		var cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);

		var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
		{
			{ "CTRL:KseGry05:_:A", postcode },
			{ "CTRL:2nvfUnaN:_", "Find" },
			{ "HID:inputs", "ICTRL:KseGry05:_:A" },
		});

		var clientSideRequest = new ClientSideRequest
		{
			RequestId = 2,
			Url = $"{_formUrl}?ebz={token}",
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", Constants.FormUrlEncoded },
				{ "cookie", cookie },
			},
			Body = requestBody,
			Options = new ClientSideOptions
			{
				Metadata =
				{
					{ "token", token },
					{ "cookie", cookie },
				},
			},
		};

		return clientSideRequest;
	}
}
