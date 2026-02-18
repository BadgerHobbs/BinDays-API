namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for North Kesteven District Council.
/// </summary>
internal sealed partial class NorthKestevenDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "North Kesteven District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.n-kesteven.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "north-kesteven";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "Household Waste",
			Colour = BinColour.Black,
			Keys = [ "Black" ],
		},
		new()
		{
			Name = "Plastic Recycling",
			Colour = BinColour.Green,
			Keys = [ "Green" ],
		},
		new()
		{
			Name = "Paper and Card Recycling",
			Colour = BinColour.Purple,
			Keys = [ "Purple" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Brown" ],
		},
	];

	/// <summary>
	/// Regex for the address options block.
	/// </summary>
	[GeneratedRegex(@"<select[^>]*name=""uprn""[^>]*>(?<options>[\s\S]*?)</select>")]
	private static partial Regex AddressSectionRegex();

	/// <summary>
	/// Regex for the addresses from the options elements.
	/// </summary>
	[GeneratedRegex(@"<option value=""(?<uprn>[^""]+)"">(?<address>[^<]+)</option>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the bin name and collection date from a list item in the bin-dates section.
	/// Matches: &lt;li&gt;&lt;span class="text-COLOUR font-weight-bold"&gt;NAME&lt;/span&gt; ... &lt;strong&gt;Day, DD Month YYYY&lt;/strong&gt;
	/// </summary>
	[GeneratedRegex(@"<li[^>]*><span[^>]*font-weight-bold[^>]*>(?<name>[^<]+)</span>[^<]*(?:<[^>]+>[^<]*</[^>]+>)*[^<]*<strong>(?<date>[^<]+)</strong>")]
	private static partial Regex BinDayRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for postcode entry page
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://www.n-kesteven.org.uk/bins",
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
		// Submit postcode to retrieve address list
		else if (clientSideResponse.RequestId == 1)
		{
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
				clientSideResponse.Headers["set-cookie"]
			);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://www.n-kesteven.org.uk/bins",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", "application/x-www-form-urlencoded" },
					{ "cookie", requestCookies },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{ "postcode", postcode },
					{ "submit", string.Empty },
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
		// Fetch address selection page
		else if (clientSideResponse.RequestId == 2)
		{
			var requestCookies = clientSideResponse.Options.Metadata["cookie"];

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = "https://www.n-kesteven.org.uk/bins/address",
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
			var addressSection = AddressSectionRegex()
				.Match(clientSideResponse.Content)
				.Groups["options"]
				.Value;

			var rawAddresses = AddressRegex().Matches(addressSection)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var uprn = rawAddress.Groups["uprn"].Value.Trim();

				if (string.IsNullOrWhiteSpace(uprn))
				{
					continue;
				}

				var property = rawAddress.Groups["address"].Value.Trim();

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

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request to establish session
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://www.n-kesteven.org.uk/bins",
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
		// Submit postcode to register it in the server session
		else if (clientSideResponse.RequestId == 1)
		{
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
				clientSideResponse.Headers["set-cookie"]
			);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://www.n-kesteven.org.uk/bins",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", "application/x-www-form-urlencoded" },
					{ "cookie", requestCookies },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{ "postcode", address.Postcode! },
					{ "submit", string.Empty },
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
		// Fetch the bin display page for the given UPRN
		else if (clientSideResponse.RequestId == 2)
		{
			var requestCookies = clientSideResponse.Options.Metadata["cookie"];

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"https://www.n-kesteven.org.uk/bins/display?uprn={address.Uid}",
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
		// Process bin days from response
		else if (clientSideResponse.RequestId == 3)
		{
			var binDays = new List<BinDay>();
			foreach (Match match in BinDayRegex().Matches(clientSideResponse.Content))
			{
				var binName = match.Groups["name"].Value.Trim();
				var dateText = match.Groups["date"].Value.Trim();

				// Date format is "Day, DD Month YYYY" (e.g. "Monday, 23 February 2026")
				var dateParts = dateText.Split(", ");
				if (dateParts.Length < 2)
				{
					continue;
				}

				var date = DateOnly.ParseExact(
					dateParts[1],
					"d MMMM yyyy",
					CultureInfo.InvariantCulture,
					DateTimeStyles.None
				);

				var matchingBins = ProcessingUtilities.GetMatchingBins(_binTypes, binName);

				var binDay = new BinDay
				{
					Address = address,
					Date = date,
					Bins = matchingBins,
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
