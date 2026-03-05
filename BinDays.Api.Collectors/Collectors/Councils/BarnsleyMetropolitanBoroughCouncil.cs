namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Barnsley Metropolitan Borough Council.
/// </summary>
internal sealed partial class BarnsleyMetropolitanBoroughCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Barnsley Metropolitan Borough Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.barnsley.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "barnsley";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Grey,
			Keys = [ "Grey" ],
		},
		new()
		{
			Name = "Paper Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Blue" ],
		},
		new()
		{
			Name = "Glass and Plastic Recycling",
			Colour = BinColour.Brown,
			Keys = [ "Brown" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Green" ],
		},
	];

	/// <summary>
	/// The URL for the address selection page.
	/// </summary>
	private const string SelectAddressUrl = "https://waste.barnsley.gov.uk/ViewCollection/SelectAddress";

	/// <summary>
	/// Regex for the RequestVerificationToken.
	/// </summary>
	[GeneratedRegex(@"<input name=""__RequestVerificationToken"" type=""hidden"" value=""(?<token>[^""]+)"" />")]
	private static partial Regex TokenRegex();

	/// <summary>
	/// Regex for the postcode value from the hidden input.
	/// </summary>
	[GeneratedRegex(@"name=""personInfo\.person1\.Postcode""[^>]*value=""(?<postcode>[^""]*)""")]
	private static partial Regex PostcodeRegex();

	/// <summary>
	/// Regex for parsing addresses from option elements.
	/// </summary>
	[GeneratedRegex(@"<option value=""(?<uid>[^""]+)"">(?<address>[^<]+)</option>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for parsing the next bin collection section.
	/// </summary>
	[GeneratedRegex(@"Your next bin collection\s*</p>\s*<p><em class=""ui-bin-next-date"">(?<date>[^<]+)</em></p>\s*<p class=""ui-bin-next-type"">\s*(?<bins>[^<]+)</p>", RegexOptions.Singleline)]
	private static partial Regex NextCollectionRegex();

	/// <summary>
	/// Regex for parsing bin collection rows from tables.
	/// </summary>
	[GeneratedRegex(@"<tr>\s*<td>\s*(?<date>[A-Za-z]+,\s+[A-Za-z]+\s+\d{1,2},\s+\d{4})\s*</td>\s*<td>\s*(?<bins>[^<]+)</td>\s*</tr>", RegexOptions.Singleline)]
	private static partial Regex BinRowRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting the form token
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = SelectAddressUrl,
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
			var token = TokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;

			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "personInfo.person1.HouseNumberOrName", string.Empty },
				{ "personInfo.person1.Postcode", postcode },
				{ "person1_FindAddress", "Find address" },
				{ "__RequestVerificationToken", token },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = SelectAddressUrl,
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", "application/x-www-form-urlencoded" },
					{ "cookie", requestCookies },
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
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var uid = rawAddress.Groups["uid"].Value;

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
		// Prepare client-side request for getting the form token
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = SelectAddressUrl,
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
		// Prepare client-side request for finding addresses
		else if (clientSideResponse.RequestId == 1)
		{
			var token = TokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;

			var setCookieHeader = clientSideResponse.Headers["set-cookie"];
			var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "personInfo.person1.HouseNumberOrName", string.Empty },
				{ "personInfo.person1.Postcode", address.Postcode! },
				{ "person1_FindAddress", "Find address" },
				{ "__RequestVerificationToken", token },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = SelectAddressUrl,
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", "application/x-www-form-urlencoded" },
					{ "cookie", requestCookies },
				},
				Body = requestBody,
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
		// Prepare client-side request for selecting the address
		else if (clientSideResponse.RequestId == 2)
		{
			var token = TokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;
			var requestCookies = clientSideResponse.Options.Metadata["cookie"];
			var postcodeValue = PostcodeRegex().Match(clientSideResponse.Content).Groups["postcode"].Value;

			var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "personInfo.person1.HouseNumberOrName", string.Empty },
				{ "personInfo.person1.Postcode", postcodeValue },
				{ "personInfo.person1.UPRN", address.Uid! },
				{ "person1_SelectAddress", "Select address" },
				{ "__RequestVerificationToken", token },
			});

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = SelectAddressUrl,
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", "application/x-www-form-urlencoded" },
					{ "cookie", requestCookies },
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
			var binDays = new List<BinDay>();

			var nextCollectionMatch = NextCollectionRegex().Match(clientSideResponse.Content);
			if (nextCollectionMatch.Success)
			{
				var date = ParseCollectionDate(nextCollectionMatch.Groups["date"].Value);

				var bins = ProcessingUtilities.GetMatchingBins(_binTypes, nextCollectionMatch.Groups["bins"].Value.Trim());

				binDays.Add(new BinDay
				{
					Date = date,
					Address = address,
					Bins = bins,
				});
			}

			var binRows = BinRowRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin day row, and create a new bin day object
			foreach (Match binRow in binRows)
			{
				var date = ParseCollectionDate(binRow.Groups["date"].Value);

				var bins = ProcessingUtilities.GetMatchingBins(_binTypes, binRow.Groups["bins"].Value.Trim());

				binDays.Add(new BinDay
				{
					Date = date,
					Address = address,
					Bins = bins,
				});
			}

			var getBinDaysResponse = new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};

			return getBinDaysResponse;
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <summary>
	/// Parses collection date strings, handling relative terms when present.
	/// </summary>
	private static DateOnly ParseCollectionDate(string value)
	{
		var dateString = value.Trim();

		if (string.Equals(dateString, "Today", StringComparison.OrdinalIgnoreCase))
		{
			return DateOnly.FromDateTime(DateTime.UtcNow);
		}

		if (string.Equals(dateString, "Tomorrow", StringComparison.OrdinalIgnoreCase))
		{
			return DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
		}

		return DateOnly.ParseExact(
			dateString,
			"dddd, MMMM d, yyyy",
			CultureInfo.InvariantCulture,
			DateTimeStyles.None
		);
	}
}
