namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Southampton City Council.
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
	private readonly IReadOnlyCollection<Bin> _binTypes = [
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Glass",
			Colour = BinColour.Grey,
			Keys = [ "Glass" ],
		},
		new()
		{
			Name = "General",
			Colour = BinColour.Green,
			Keys = [ "General" ],
		},
		new()
		{
			Name = "Garden",
			Colour = BinColour.Brown,
			Keys = [ "Garden" ],
		},
	];

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
	[GeneratedRegex(@"<option\s+value=""(?<uid>\d+),\d*""[^>]*>\s*(?<address>.*?)\s*</option>")]
	private static partial Regex AddressesRegex();

	/// <summary>
	/// Regex for the Imperva challenge resource path.
	/// </summary>
	[GeneratedRegex(@"src=""(?<resource>/_Incapsula_Resource[^""]+)""")]
	private static partial Regex IncapsulaResourceRegex();

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
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://www.southampton.gov.uk/bins-recycling/bins/collections/",
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

			var addressLookupRequest = CreateAddressLookupRequest(postcode, clientSideResponse.Content, requestCookies, 4);
			if (addressLookupRequest != null)
			{
				var immediateGetAddressesResponse = new GetAddressesResponse
				{
					NextClientSideRequest = addressLookupRequest,
				};

				return immediateGetAddressesResponse;
			}

			var incapsulaResourceMatch = IncapsulaResourceRegex().Match(clientSideResponse.Content);
			if (!incapsulaResourceMatch.Success)
			{
				throw new InvalidOperationException("Could not find required '__RequestVerificationToken' for address lookup.");
			}

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"https://www.southampton.gov.uk{incapsulaResourceMatch.Groups["resource"].Value}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8" },
					{ "accept-language", "en-GB,en;q=0.5" },
					{ "accept-encoding", "gzip, deflate, br" },
					{ "cookie", requestCookies },
					{ "referer", "https://www.southampton.gov.uk/bins-recycling/bins/collections/" },
					{ "upgrade-insecure-requests", "1" },
					{ "sec-fetch-dest", "iframe" },
					{ "sec-fetch-mode", "navigate" },
					{ "sec-fetch-site", "same-origin" },
				},
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", requestCookies },
					},
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for reloading the collections page after challenge bootstrap
		else if (clientSideResponse.RequestId == 2)
		{
			var requestCookies = clientSideResponse.Options.Metadata["cookie"];

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = "https://www.southampton.gov.uk/bins-recycling/bins/collections/",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8" },
					{ "accept-language", "en-GB,en;q=0.5" },
					{ "accept-encoding", "gzip, deflate, br" },
					{ "cookie", requestCookies },
					{ "referer", "https://www.southampton.gov.uk/bins-recycling/bins/collections/" },
					{ "upgrade-insecure-requests", "1" },
					{ "sec-fetch-dest", "document" },
					{ "sec-fetch-mode", "navigate" },
					{ "sec-fetch-site", "same-origin" },
					{ "sec-fetch-user", "?1" },
				},
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", requestCookies },
					},
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for getting addresses
		else if (clientSideResponse.RequestId == 3)
		{
			var requestCookies = clientSideResponse.Options.Metadata["cookie"];
			if (clientSideResponse.Headers.TryGetValue("set-cookie", out var setCookieHeader))
			{
				var responseCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);
				requestCookies = $"{requestCookies}; {responseCookies}";
			}

			var addressLookupRequest = CreateAddressLookupRequest(postcode, clientSideResponse.Content, requestCookies, 4) ?? throw new InvalidOperationException("Could not find required '__RequestVerificationToken' for address lookup.");
			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = addressLookupRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 4)
		{
			// Get addresses from response
			var rawAddresses = AddressesRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var property = rawAddress.Groups["address"].Value;
				var uprn = rawAddress.Groups["uid"].Value;

				var address = new Address
				{
					Property = property,
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

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting bin days
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://www.southampton.gov.uk/whereilive/waste-calendar?UPRN={address.Uid}",
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 1)
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
				var date = DateUtilities.ParseDateExact(collectionDate, "M/d/yyyy");

				// Get matching bin types from the service using the keys
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
	/// Creates the address lookup POST request when anti-forgery tokens are available.
	/// </summary>
	private static ClientSideRequest? CreateAddressLookupRequest(string postcode, string content, string requestCookies, int requestId)
	{
		var ufprtMatch = UfprtTokenRegex().Match(content);
		var requestVerificationTokenMatch = RequestVerificationTokenRegex().Match(content);
		if (!ufprtMatch.Success || !requestVerificationTokenMatch.Success)
		{
			return null;
		}

		var ufprt = ufprtMatch.Groups["ufprt"].Value;
		var requestVerificationToken = requestVerificationTokenMatch.Groups["token"].Value;

		var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
		{
			{ "SearchString", postcode },
			{ "ufprt", ufprt },
			{ "__RequestVerificationToken", requestVerificationToken },
		});

		var clientSideRequest = new ClientSideRequest
		{
			RequestId = requestId,
			Url = "https://www.southampton.gov.uk/bins-recycling/bins/collections/",
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", Constants.FormUrlEncoded },
				{ "cookie", requestCookies },
			},
			Body = requestBody,
		};

		return clientSideRequest;
	}
}
