namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Stockport Metropolitan Borough Council.
/// </summary>
internal sealed partial class StockportMetropolitanBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Stockport Metropolitan Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.stockport.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "stockport";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Black bin", "Black" ],
		},
		new()
		{
			Name = "Garden and Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Green bin", "Garden", "Food" ],
		},
		new()
		{
			Name = "Waste Paper Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Blue bin", "Paper", "Cardboard", "Cartons" ],
		},
		new()
		{
			Name = "Glass, Aluminium and Plastic Recycling",
			Colour = BinColour.Brown,
			Keys = [ "Brown bin", "Plastic", "Glass", "Tins", "Cans", "Aerosols" ],
		},
	];

	/// <summary>
	/// Regex for extracting the request verification token.
	/// </summary>
	[GeneratedRegex(@"__RequestVerificationToken"" type=""hidden"" value=""(?<token>[^""]+)""")]
	private static partial Regex TokenRegex();

	/// <summary>
	/// Regex for parsing addresses from the select options.
	/// </summary>
	[GeneratedRegex(@"<option value=""(?<uid>[^""]+)"">(?<address>[^<]+)</option>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for extracting the bin collection URL.
	/// </summary>
	[GeneratedRegex(@"href=['""](?<url>https://myaccount\.stockport\.gov\.uk/bin-collections/show/[^'""]+)['""]", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
	private static partial Regex BinCollectionsUrlRegex();

	/// <summary>
	/// Regex for parsing bin names, descriptions, and dates.
	/// </summary>
	[GeneratedRegex(@"service-item[^>]*>\s*<img[^>]*>\s*<div>\s*<h3>(?<name>[^<]+)</h3>.*?class=""sub-title"">(?<description>[^<]+)</p>\s*<p>\s*(?<date>[^<]+)</p", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting initial form cookies
		if (clientSideResponse == null)
		{
			var formattedPostcode = ProcessingUtilities.FormatPostcode(postcode);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://forms.stockport.gov.uk/bin-collections",
				Method = "GET",
				Headers = new()
				{
					{ "User-Agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "postcode", formattedPostcode },
					},
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for getting the address form
		else if (clientSideResponse.RequestId == 1)
		{
			clientSideResponse.Headers.TryGetValue("set-cookie", out var setCookieHeader);
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader ?? string.Empty);
			var postcodeMetadata = clientSideResponse.Options.Metadata["postcode"];

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://forms.stockport.gov.uk/bin-collections/address",
				Method = "GET",
				Headers = new()
				{
					{ "User-Agent", Constants.UserAgent },
					{ "Cookie", cookies },
				},
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "postcode", postcodeMetadata },
						{ "cookie", cookies },
					},
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for posting the postcode
		else if (clientSideResponse.RequestId == 2)
		{
			var token = TokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;

			var postcodeMetadata = clientSideResponse.Options.Metadata["postcode"];
			var baseCookie = clientSideResponse.Options.Metadata["cookie"];

			clientSideResponse.Headers.TryGetValue("set-cookie", out var setCookieHeader);
			var tokenCookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader ?? string.Empty);

			var cookies = string.Join(
				"; ",
				new[] { baseCookie, tokenCookie }.Where(x => !string.IsNullOrWhiteSpace(x))
			);

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "__RequestVerificationToken", token },
				{ "yourAddress-postcode", postcodeMetadata },
				{ "Path", "address" },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = "https://forms.stockport.gov.uk/bin-collections/address",
				Method = "POST",
				Headers = new()
				{
					{ "User-Agent", Constants.UserAgent },
					{ "Content-Type", "application/x-www-form-urlencoded" },
					{ "Cookie", cookies },
				},
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "postcode", postcodeMetadata },
						{ "cookie", cookies },
					},
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for getting the addresses
		else if (clientSideResponse.RequestId == 3)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 4,
				Url = "https://forms.stockport.gov.uk/bin-collections/address/automatic",
				Method = "GET",
				Headers = new()
				{
					{ "User-Agent", Constants.UserAgent },
					{ "Cookie", clientSideResponse.Options.Metadata["cookie"] },
				},
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "postcode", clientSideResponse.Options.Metadata["postcode"] },
					},
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 4)
		{
			// Get addresses from response
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var uid = rawAddress.Groups["uid"].Value.Trim();

				if (string.IsNullOrWhiteSpace(uid))
				{
					continue;
				}

				var address = new Address
				{
					Property = rawAddress.Groups["address"].Value.Trim(),
					Postcode = clientSideResponse.Options.Metadata["postcode"],
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
		// Prepare client-side request for getting initial form cookies
		if (clientSideResponse == null)
		{
			var formattedPostcode = ProcessingUtilities.FormatPostcode(address.Postcode!);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://forms.stockport.gov.uk/bin-collections",
				Method = "GET",
				Headers = new()
				{
					{ "User-Agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "postcode", formattedPostcode },
						{ "uid", address.Uid! },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting the address form
		else if (clientSideResponse.RequestId == 1)
		{
			clientSideResponse.Headers.TryGetValue("set-cookie", out var setCookieHeader);
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader ?? string.Empty);

			var postcodeMetadata = clientSideResponse.Options.Metadata["postcode"];
			var uid = clientSideResponse.Options.Metadata["uid"];

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://forms.stockport.gov.uk/bin-collections/address",
				Method = "GET",
				Headers = new()
				{
					{ "User-Agent", Constants.UserAgent },
					{ "Cookie", cookies },
				},
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "postcode", postcodeMetadata },
						{ "cookie", cookies },
						{ "uid", uid },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for posting the postcode
		else if (clientSideResponse.RequestId == 2)
		{
			var token = TokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;

			var postcodeMetadata = clientSideResponse.Options.Metadata["postcode"];
			var baseCookie = clientSideResponse.Options.Metadata["cookie"];
			var uid = clientSideResponse.Options.Metadata["uid"];

			clientSideResponse.Headers.TryGetValue("set-cookie", out var setCookieHeader);
			var tokenCookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader ?? string.Empty);

			var cookies = string.Join(
				"; ",
				new[] { baseCookie, tokenCookie }.Where(x => !string.IsNullOrWhiteSpace(x))
			);

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "__RequestVerificationToken", token },
				{ "yourAddress-postcode", postcodeMetadata },
				{ "Path", "address" },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = "https://forms.stockport.gov.uk/bin-collections/address",
				Method = "POST",
				Headers = new()
				{
					{ "User-Agent", Constants.UserAgent },
					{ "Content-Type", "application/x-www-form-urlencoded" },
					{ "Cookie", cookies },
				},
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "postcode", postcodeMetadata },
						{ "cookie", cookies },
						{ "uid", uid },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting the address list
		else if (clientSideResponse.RequestId == 3)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 4,
				Url = "https://forms.stockport.gov.uk/bin-collections/address/automatic",
				Method = "GET",
				Headers = new()
				{
					{ "User-Agent", Constants.UserAgent },
					{ "Cookie", clientSideResponse.Options.Metadata["cookie"] },
				},
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "postcode", clientSideResponse.Options.Metadata["postcode"] },
						{ "cookie", clientSideResponse.Options.Metadata["cookie"] },
						{ "uid", clientSideResponse.Options.Metadata["uid"] },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for posting the selected address
		else if (clientSideResponse.RequestId == 4)
		{
			var token = TokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;

			var postcodeMetadata = clientSideResponse.Options.Metadata["postcode"];
			var cookies = clientSideResponse.Options.Metadata["cookie"];
			var uid = clientSideResponse.Options.Metadata["uid"];

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "__RequestVerificationToken", token },
				{ "yourAddress-postcode", postcodeMetadata },
				{ "yourAddress-address", uid },
				{ "Path", "address" },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 5,
				Url = "https://forms.stockport.gov.uk/bin-collections/address/automatic",
				Method = "POST",
				Headers = new()
				{
					{ "User-Agent", Constants.UserAgent },
					{ "Content-Type", "application/x-www-form-urlencoded" },
					{ "Cookie", cookies },
				},
				Body = requestBody,
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", cookies },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting the confirmation page
		else if (clientSideResponse.RequestId == 5)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 6,
				Url = "https://forms.stockport.gov.uk/bin-collections/bin-collections",
				Method = "GET",
				Headers = new()
				{
					{ "User-Agent", Constants.UserAgent },
					{ "Cookie", clientSideResponse.Options.Metadata["cookie"] },
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting the bin collections page
		else if (clientSideResponse.RequestId == 6)
		{
			var binCollectionsUrl = BinCollectionsUrlRegex().Match(clientSideResponse.Content).Groups["url"].Value;

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 7,
				Url = binCollectionsUrl,
				Method = "GET",
				Headers = new()
				{
					{ "User-Agent", Constants.UserAgent },
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 7)
		{
			// Get bin days from response
			var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var serviceName = rawBinDay.Groups["name"].Value.Trim();
				var description = rawBinDay.Groups["description"].Value.Trim();
				var dateString = rawBinDay.Groups["date"].Value.Trim();

				var date = DateOnly.ParseExact(
					dateString,
					"dddd, d MMMM yyyy",
					CultureInfo.InvariantCulture,
					DateTimeStyles.None
				);

				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, $"{serviceName} {description}");

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
