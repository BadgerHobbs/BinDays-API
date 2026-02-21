namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for East Lothian Council.
/// </summary>
internal sealed partial class EastLothianCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "East Lothian Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.eastlothian.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "east-lothian";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "Food waste and recycling",
			Colour = BinColour.Grey,
			Keys = [ "Food waste and recycling" ],
		},
		new()
		{
			Name = "Non recyclable waste",
			Colour = BinColour.Green,
			Keys = [ "Non recyclable waste" ],
		},
	];

	/// <summary>
	/// Regex for the form build id.
	/// </summary>
	[GeneratedRegex(@"name=""form_build_id""\s+value=""(?<formBuildId>[^""]+)""")]
	private static partial Regex FormBuildIdRegex();

	/// <summary>
	/// Regex for the addresses from the data.
	/// </summary>
	[GeneratedRegex(@"<option value=""(?<uid>[^""]*)"">(?<address>[^<]+)</option>")]
	private static partial Regex AddressRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://collectiondates.eastlothian.gov.uk/waste-collection-schedule",
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
		else if (clientSideResponse.RequestId == 1)
		{
			// Prepare client-side request for posting the postcode
			var formBuildId = FormBuildIdRegex().Match(clientSideResponse.Content).Groups["formBuildId"].Value;

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = "https://collectiondates.eastlothian.gov.uk/waste-collection-schedule",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", "application/x-www-form-urlencoded" },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{ "postcode", postcode },
					{ "op", "Find" },
					{ "form_build_id", formBuildId },
					{ "form_id", "localgov_waste_collection_postcode_form" },
				}),
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		else if (clientSideResponse.RequestId == 2)
		{
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
		// Prepare client-side request for getting the calendar file
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://collectiondates.eastlothian.gov.uk/waste-collection-schedule/download/{address.Uid!}",
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
		else if (clientSideResponse.RequestId == 1)
		{
			var unfoldedLines = new List<string>();
			var lines = clientSideResponse.Content.Split('\n');

			foreach (var line in lines)
			{
				var trimmedLine = line.TrimEnd('\r');

				if (trimmedLine.StartsWith(' ') && unfoldedLines.Count > 0)
				{
					unfoldedLines[^1] += trimmedLine[1..];
					continue;
				}

				unfoldedLines.Add(trimmedLine);
			}

			// Iterate through each event, and create a new bin day object
			var binDays = new List<BinDay>();
			var summary = string.Empty;
			var dateString = string.Empty;

			foreach (var line in unfoldedLines)
			{
				if (line == "BEGIN:VEVENT")
				{
					summary = string.Empty;
					dateString = string.Empty;
					continue;
				}

				if (line.StartsWith("SUMMARY:", StringComparison.Ordinal))
				{
					summary = line["SUMMARY:".Length..].Trim();
					continue;
				}

				if (line.StartsWith("DTSTART", StringComparison.Ordinal))
				{
					var value = line[(line.LastIndexOf(':') + 1)..];
					dateString = value[..8];
					continue;
				}

				if (line != "END:VEVENT" || !summary.Contains("lidded containers for", StringComparison.Ordinal))
				{
					continue;
				}

				var serviceStartIndex = summary.LastIndexOf(" for ", StringComparison.Ordinal) + 5;
				var service = summary[serviceStartIndex..];

				var date = DateOnly.ParseExact(
					dateString,
					"yyyyMMdd",
					CultureInfo.InvariantCulture,
					DateTimeStyles.None
				);

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
