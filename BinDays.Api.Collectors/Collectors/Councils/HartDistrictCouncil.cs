namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Hart District Council.
/// </summary>
internal sealed partial class HartDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Hart District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.hart.gov.uk/your-area");

	/// <inheritdoc/>
	public override string GovUkId => "hart";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Refuse", ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Black,
			Keys = [ "Recycling", ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Garden", ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Keys = [ "Food Waste", ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// The shared AJAX request body used for postcode and collection lookups.
	/// </summary>
	private const string _ajaxRequestBody = "js=true&_drupal_ajax=1&ajax_page_state%5Btheme%5D=bbd_localgov&ajax_page_state%5Btheme_token%5D=&ajax_page_state%5Blibraries%5D=eJyFkQFuwyAMRS9E4EjIBJewGIwwtMvtR9pKbdN1kxDY_33AYPC-MeTNwD3Qp8q5KQfzahuPUcxTbL_kI2r4PfY5bynmNTZbqIeYxbymV8cF3YlrMvfVLkjFFi693PASG0qBGc1rqrDbmXmNOJZUKEIent9E66CiOo2nwAWFE5qnWF_vzU1UYA6EFjLQ1uIs5igo4hko8HkcKKOciuDn2pM7kv0dE9bK9Y10ooMUiB0cxYS5TwliPugFAk4tNsLJDbIesIwy121aRmFYH0y6k_3bbIEKoUJZRif-gOrJ52svQPqh6J5LdxRlQa-E5whkE_oI116LeZd0WzChkk0aJrMXqg5N10jDkZv22CCSaIHz_6bGYfTnoy2hyPiuN75PP7JZJlg";

	/// <summary>
	/// Regex for parsing address options from the response HTML.
	/// </summary>
	[GeneratedRegex(@"<option[^>]*class=""address-item""[^>]*value=""(?<uid>[^""]+)""[^>]*>(?<address>[^<]+)</option>", RegexOptions.Singleline)]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for parsing bin day rows from the response HTML.
	/// </summary>
	[GeneratedRegex(@"<tr>.*?<td class=""bin-service[^""]*"">(?<service>.*?)</td>.*?<td class=""bin-service-date"">(?<date>[^<]+)</td>.*?</tr>", RegexOptions.Singleline)]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for the postcode search
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://www.hart.gov.uk/bbd-whitespace/list-addresses?postalCode={postcode}&_wrapper_format=drupal_ajax",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", "application/x-www-form-urlencoded; charset=UTF-8" },
					{ "x-requested-with", "XMLHttpRequest" },
				},
				Body = _ajaxRequestBody,
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from the AJAX response
		else if (clientSideResponse.RequestId == 1)
		{
			using var document = JsonDocument.Parse(clientSideResponse.Content);
			var addressHtml = document.RootElement[1].GetProperty("data").GetString()!;
			var decodedHtml = WebUtility.HtmlDecode(addressHtml);

			var rawAddresses = AddressRegex().Matches(decodedHtml)!;

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
		// Prepare client-side request for bin collection dates
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://www.hart.gov.uk/bbd-whitespace/next-collection-dates?uprn={address.Uid!}&uri=entity%3Anode%2F172&text=View%20the%20calendar&_wrapper_format=drupal_ajax",
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", "application/x-www-form-urlencoded; charset=UTF-8" },
					{ "x-requested-with", "XMLHttpRequest" },
				},
				Body = _ajaxRequestBody,
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin collection dates from the AJAX response
		else if (clientSideResponse.RequestId == 1)
		{
			using var document = JsonDocument.Parse(clientSideResponse.Content);
			var binDaysHtml = document.RootElement[0].GetProperty("data").GetString()!;
			var decodedHtml = WebUtility.HtmlDecode(binDaysHtml);

			var rawBinDays = BinDaysRegex().Matches(decodedHtml)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["service"].Value.Trim();
				var serviceDate = rawBinDay.Groups["date"].Value.Trim();

				var date = serviceDate.ParseDateInferringYear(
					"d MMMM"
				);

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

		throw new InvalidOperationException("Invalid client-side request.");
	}
}
