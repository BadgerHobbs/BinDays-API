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
			Keys = [ "Black (Domestic)" ],
		},
		new()
		{
			Name = "Plastic Recycling",
			Colour = BinColour.Green,
			Keys = [ "Green (Recycling)" ],
		},
		new()
		{
			Name = "Paper and Card Recycling",
			Colour = BinColour.Purple,
			Keys = [ "Purple (Paper/Card)" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Brown (Garden Waste)" ],
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
	/// Regex for the bin sections containing next and future collections.
	/// </summary>
	[GeneratedRegex(@"<div class=""bg-[^""]+ bin-[^""]+-next[\s\S]*?<h3>(?<service>[^<]+)</h3>[\s\S]*?Next Collection:\s*(?<next>[^<]+)</strong>[\s\S]*?future-bin-dates[^>]*>[\s\S]*?<ul[^>]*>(?<future>[\s\S]*?)</ul>")]
	private static partial Regex BinSectionRegex();

	/// <summary>
	/// Regex for the collection dates.
	/// </summary>
	[GeneratedRegex(@"(?<date>[A-Za-z]+,\s+\d{1,2}\s+[A-Za-z]+\s+\d{4})")]
	private static partial Regex DateRegex();

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
		// Prepare client-side request for bin details page
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
		// Submit the selected address to set the session
		else if (clientSideResponse.RequestId == 1)
		{
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
				clientSideResponse.Headers["set-cookie"]
			);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://www.n-kesteven.org.uk/bins/address",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", "application/x-www-form-urlencoded" },
					{ "cookie", requestCookies },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{ "uprn", address.Uid! },
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
		// Prepare client-side request for bin details page
		else if (clientSideResponse.RequestId == 2)
		{
			var requestCookies = clientSideResponse.Options.Metadata["cookie"];

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = $"https://www.n-kesteven.org.uk/bins/details?uprn={address.Uid}",
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
			var binSections = BinSectionRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin section, and create bin day entries
			var binDays = new List<BinDay>();
			foreach (Match binSection in binSections)
			{
				var service = binSection.Groups["service"].Value.Trim();
				var nextCollection = binSection.Groups["next"].Value.Trim();
				var futureCollections = binSection.Groups["future"].Value;

				var matchingBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);
				var collectionDates = new HashSet<DateOnly>();

				var nextDateMatch = DateRegex().Match(nextCollection);
				if (nextDateMatch.Success)
				{
					var date = DateOnly.ParseExact(
						nextDateMatch.Groups["date"].Value,
						"dddd, d MMMM yyyy",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);

					collectionDates.Add(date);
				}

				// Iterate through each future collection date, and add it to the set
				foreach (Match dateMatch in DateRegex().Matches(futureCollections)!)
				{
					var date = DateOnly.ParseExact(
						dateMatch.Groups["date"].Value,
						"dddd, d MMMM yyyy",
						CultureInfo.InvariantCulture,
						DateTimeStyles.None
					);

					collectionDates.Add(date);
				}

				// Iterate through each collection date, and create a bin day object
				foreach (var collectionDate in collectionDates)
				{
					var binDay = new BinDay
					{
						Address = address,
						Date = collectionDate,
						Bins = matchingBins,
					};

					binDays.Add(binDay);
				}
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
