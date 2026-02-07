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
			Keys = [ "Black" ],
		},
		new()
		{
			Name = "Garden and Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Green" ],
		},
		new()
		{
			Name = "Waste Paper Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Blue" ],
		},
		new()
		{
			Name = "Glass, Aluminium and Plastic Recycling",
			Colour = BinColour.Brown,
			Keys = [ "Brown" ],
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
	[GeneratedRegex(@"service-item[^>]*>\s*<img[^>]*>\s*<div>\s*<h3>(?<name>[^<]+)</h3>.*?class=""sub-title"">(?<description>[^<]+)</p>\s*<p>\s*(?<date>[^<]+)</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Use helper for initial form steps (RequestId null, 1, 2, 3)
		if (clientSideResponse == null || clientSideResponse.RequestId <= 3)
		{
			var clientSideRequest = HandleInitialFormSteps(clientSideResponse, postcode, 4);

			if (clientSideRequest != null)
			{
				return new GetAddressesResponse
				{
					NextClientSideRequest = clientSideRequest,
				};
			}
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
					Postcode = postcode,
					Uid = uid,
				};

				addresses.Add(address);
			}

			return new GetAddressesResponse
			{
				Addresses = [.. addresses],
			};
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Use helper for initial form steps (RequestId null, 1, 2, 3)
		if (clientSideResponse == null || clientSideResponse.RequestId <= 3)
		{
			var clientSideRequest = HandleInitialFormSteps(clientSideResponse, address.Postcode!, 4);

			if (clientSideRequest != null)
			{
				return new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest,
				};
			}
		}
		// Prepare client-side request for posting the selected address
		else if (clientSideResponse.RequestId == 4)
		{
			var token = TokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;
			var cookies = clientSideResponse.Options.Metadata["cookie"];

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "__RequestVerificationToken", token },
				{ "yourAddress-postcode", address.Postcode! },
				{ "yourAddress-address", address.Uid! },
				{ "Path", "address" },
			});

			return new GetBinDaysResponse
			{
				NextClientSideRequest = new ClientSideRequest
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
				},
			};
		}
		// Prepare client-side request for getting the confirmation page
		else if (clientSideResponse.RequestId == 5)
		{
			return new GetBinDaysResponse
			{
				NextClientSideRequest = new ClientSideRequest
				{
					RequestId = 6,
					Url = "https://forms.stockport.gov.uk/bin-collections/bin-collections",
					Method = "GET",
					Headers = new()
					{
						{ "User-Agent", Constants.UserAgent },
						{ "Cookie", clientSideResponse.Options.Metadata["cookie"] },
					},
				},
			};
		}
		// Prepare client-side request for getting the bin collections page
		else if (clientSideResponse.RequestId == 6)
		{
			var binCollectionsUrl = BinCollectionsUrlRegex().Match(clientSideResponse.Content).Groups["url"].Value;

			return new GetBinDaysResponse
			{
				NextClientSideRequest = new ClientSideRequest
				{
					RequestId = 7,
					Url = binCollectionsUrl,
					Method = "GET",
					Headers = new()
					{
						{ "User-Agent", Constants.UserAgent },
					},
				},
			};
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

			return new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <summary>
	/// Helper method to handle the initial form steps to acquire session cookies and tokens.
	/// </summary>
	/// <param name="clientSideResponse">The client-side response from the previous request.</param>
	/// <param name="postcode">The postcode to use for the form.</param>
	/// <param name="nextRequestId">The next request ID to use.</param>
	/// <returns>A client-side request for the next step, or null if we're past the initial steps.</returns>
	private static ClientSideRequest? HandleInitialFormSteps(
		ClientSideResponse? clientSideResponse,
		string postcode,
		int nextRequestId)
	{
		// Prepare client-side request for getting initial form cookies
		if (clientSideResponse == null)
		{
			return new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://forms.stockport.gov.uk/bin-collections",
				Method = "GET",
				Headers = new()
				{
					{ "User-Agent", Constants.UserAgent },
				},
			};
		}
		// Prepare client-side request for getting the address form
		else if (clientSideResponse.RequestId == 1)
		{
			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader!);

			return new ClientSideRequest
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
						{ "cookie", cookies },
					},
				},
			};
		}
		// Prepare client-side request for posting the postcode
		else if (clientSideResponse.RequestId == 2)
		{
			var token = TokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;
			var baseCookie = clientSideResponse.Options.Metadata["cookie"];

			var cookies = baseCookie;
			if (clientSideResponse.Headers.TryGetValue("set-cookie", out var value))
			{
				var tokenCookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(value!);
				cookies = string.Join("; ", new[] { baseCookie, tokenCookie }.Where(x => !string.IsNullOrWhiteSpace(x)));
			}

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "__RequestVerificationToken", token },
				{ "yourAddress-postcode", postcode },
				{ "Path", "address" },
			});

			return new ClientSideRequest
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
						{ "cookie", cookies },
					},
				},
			};
		}
		// Prepare client-side request for getting the addresses
		else if (clientSideResponse.RequestId == 3)
		{
			var cookies = clientSideResponse.Options.Metadata["cookie"];

			return new ClientSideRequest
			{
				RequestId = nextRequestId,
				Url = "https://forms.stockport.gov.uk/bin-collections/address/automatic",
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
						{ "cookie", cookies },
					},
				},
			};
		}

		return null;
	}
}
