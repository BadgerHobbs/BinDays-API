namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Cherwell District Council.
/// </summary>
internal sealed partial class CherwellDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Cherwell District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.cherwell.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "cherwell";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "Food and Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Brown Bin" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Blue Bin" ],
		},
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "Green Bin" ],
		},
	];

	/// <summary>
	/// Regex for the bin collection items on the results page.
	/// </summary>
	[GeneratedRegex(
		"""<h3 class="bin-collection-tasks__heading">(?:<span class="visually-hidden">Your next </span>)?(?<service>[^<]+)<span class="visually-hidden"> collection</span></h3>\s*<div class="bin-collection-tasks__content">\s*<p class="bin-collection-tasks__day">[^<]+</p>\s*<p class="bin-collection-tasks__date">(?<date>[^<]+)</p>""",
		RegexOptions.Singleline
	)]
	private static partial Regex BinCollectionsRegex();

	/// <summary>
	/// Regex for removing ordinal suffixes from dates.
	/// </summary>
	[GeneratedRegex(@"(?<=\d)(st|nd|rd|th)")]
	private static partial Regex OrdinalSuffixRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://forms.cherwell.gov.uk/site/custom_scripts/bartec_postcode_lookup.php",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", "application/x-www-form-urlencoded" },
				},
				Body = $"postcode={postcode}",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var addressElement in jsonDocument.RootElement.EnumerateArray())
			{
				var uprn = addressElement.GetProperty("uprn").GetString()!;
				var label = addressElement.GetProperty("label").GetString()!;

				var address = new Address
				{
					Property = label.Trim(),
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
				Url = $"https://www.cherwell.gov.uk/homepage/129/bin-collection-search?uprn={address.Uid!}",
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
		else if (clientSideResponse.RequestId == 1)
		{
			var rawCollections = BinCollectionsRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin collection, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawCollection in rawCollections)
			{
				var service = rawCollection.Groups["service"].Value.Trim();
				var dateString = rawCollection.Groups["date"].Value.Trim();

				var cleanedDate = OrdinalSuffixRegex().Replace(dateString, string.Empty);
				var date = cleanedDate.ParseDateInferringYear("d MMMM");

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
}
