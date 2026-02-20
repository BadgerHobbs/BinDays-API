namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Stockport Council.
/// </summary>
internal sealed partial class StockportCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Stockport Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.stockport.gov.uk/topic/bins-and-recycling");

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
			Keys = [ "Black bin" ],
		},
		new()
		{
			Name = "Garden and Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Green bin" ],
		},
		new()
		{
			Name = "Paper, Cardboard and Cartons",
			Colour = BinColour.Blue,
			Keys = [ "Blue bin" ],
		},
		new()
		{
			Name = "Plastic, Glass, Tins, Cans and Aerosols",
			Colour = BinColour.Brown,
			Keys = [ "Brown bin" ],
		},
	];

	/// <summary>
	/// Regex for the RequestVerificationToken.
	/// </summary>
	[GeneratedRegex(@"<input name=""__RequestVerificationToken"" type=""hidden"" value=""(?<token>[^""]+)"" />")]
	private static partial Regex TokenRegex();

	/// <summary>
	/// Regex for parsing addresses from the options.
	/// </summary>
	[GeneratedRegex(
		@"<option[^>]*value=""(?<value>[^""]*)""[^>]*>(?<address>[^<]+)</option>",
		RegexOptions.IgnoreCase | RegexOptions.Singleline
	)]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for parsing the bin collections link.
	/// </summary>
	[GeneratedRegex(
		@"href='(?<url>https://myaccount\.stockport\.gov\.uk/bin-collections/show/[^']+)'",
		RegexOptions.IgnoreCase
	)]
	private static partial Regex BinCollectionsLinkRegex();

	/// <summary>
	/// Regex for parsing bin names, descriptions, and dates.
	/// </summary>
	[GeneratedRegex(
		@"<div class=""service-item[^""]*"">\s*<img[^>]+>\s*<div>\s*<h3>(?<name>[^<]+)</h3>\s*<p class=""sub-title"">(?<description>[^<]+)</p>\s*<p>\s*(?<date>[^<]+)</p>",
		RegexOptions.Singleline
	)]
	private static partial Regex BinDayRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting the initial page
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://forms.stockport.gov.uk/bin-collections/address",
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
		// Prepare client-side request for submitting the postcode
		else if (clientSideResponse.RequestId == 1)
		{
			var token = TokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
				clientSideResponse.Headers["set-cookie"]
			);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://forms.stockport.gov.uk/bin-collections/address",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", "application/x-www-form-urlencoded" },
					{ "cookie", requestCookies },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{ "__RequestVerificationToken", token },
					{ "yourAddress-postcode", postcode },
					{ "Path", "address" },
				}),
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
		// Prepare client-side request for retrieving the address list
		else if (clientSideResponse.RequestId == 2)
		{
			var requestCookies = clientSideResponse.Options!.Metadata["cookie"];

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = "https://forms.stockport.gov.uk/bin-collections/address/automatic",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "cookie", requestCookies },
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
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var value = rawAddress.Groups["value"].Value.Trim();
				if (string.IsNullOrWhiteSpace(value))
				{
					continue;
				}

				var property = rawAddress.Groups["address"].Value.Trim();

				var address = new Address
				{
					Property = property,
					Postcode = postcode,
					Uid = value,
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
		// Prepare client-side request for getting the initial page
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://forms.stockport.gov.uk/bin-collections/address",
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
		// Prepare client-side request for submitting the postcode
		else if (clientSideResponse.RequestId == 1)
		{
			var token = TokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
				clientSideResponse.Headers["set-cookie"]
			);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://forms.stockport.gov.uk/bin-collections/address",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", "application/x-www-form-urlencoded" },
					{ "cookie", requestCookies },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{ "__RequestVerificationToken", token },
					{ "yourAddress-postcode", address.Postcode! },
					{ "Path", "address" },
				}),
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", requestCookies },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for getting the address selection page
		else if (clientSideResponse.RequestId == 2)
		{
			var requestCookies = clientSideResponse.Options!.Metadata["cookie"];

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = "https://forms.stockport.gov.uk/bin-collections/address/automatic",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "cookie", requestCookies },
				},
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", requestCookies },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for submitting the selected address
		else if (clientSideResponse.RequestId == 3)
		{
			var requestCookies = clientSideResponse.Options!.Metadata["cookie"];
			var token = TokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 4,
				Url = "https://forms.stockport.gov.uk/bin-collections/address/automatic",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", "application/x-www-form-urlencoded" },
					{ "cookie", requestCookies },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{ "__RequestVerificationToken", token },
					{ "yourAddress-postcode", address.Postcode! },
					{ "yourAddress-address", address.Uid! },
					{ "Path", "address" },
				}),
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "cookie", requestCookies },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for the confirmation page
		else if (clientSideResponse.RequestId == 4)
		{
			var requestCookies = clientSideResponse.Options!.Metadata["cookie"];

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 5,
				Url = "https://forms.stockport.gov.uk/bin-collections/bin-collections",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "cookie", requestCookies },
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for the bin collections page
		else if (clientSideResponse.RequestId == 5)
		{
			var myAccountUrl = BinCollectionsLinkRegex().Match(clientSideResponse.Content).Groups["url"].Value;

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 6,
				Url = myAccountUrl,
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
		// Process bin days from response
		else if (clientSideResponse.RequestId == 6)
		{
			var rawBinDays = BinDayRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var serviceName = rawBinDay.Groups["name"].Value.Trim();
				var serviceDescription = rawBinDay.Groups["description"].Value.Trim();
				var dateString = rawBinDay.Groups["date"].Value.Trim();

				var date = DateOnly.ParseExact(
					dateString,
					"dddd, d MMMM yyyy",
					CultureInfo.InvariantCulture,
					DateTimeStyles.None
				);

				var service = $"{serviceName} {serviceDescription}";
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
