namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for Rochford District Council.
/// </summary>
internal sealed partial class RochfordDistrictCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Rochford District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.rochford.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "rochford";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "Non-Recyclable Waste",
			Colour = BinColour.Purple,
			Keys = [ "Non-recyclables" ],
		},
		new()
		{
			Name = "Mixed Recycling",
			Colour = BinColour.Black,
			Keys = [ "Recyclables" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "Compost" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Compost" ],
			Type = BinType.Caddy,
		},
	];

	/// <summary>
	/// The bin collection page URL.
	/// </summary>
	private const string _binCollectionUrl = "https://www.rochford.gov.uk/bins-and-collections";

	/// <summary>
	/// The Drupal AJAX endpoint for bin collection form submissions.
	/// </summary>
	private const string _binCollectionAjaxUrl = "https://www.rochford.gov.uk/bins-and-collections?ajax_form=1&_wrapper_format=drupal_ajax";

	/// <summary>
	/// The Drupal form identifier.
	/// </summary>
	private const string _formId = "waste_collection_block_ajax_form";

	/// <summary>
	/// Regex for the form build id.
	/// </summary>
	[GeneratedRegex(@"name=""form_build_id""[\s\S]*?value=""(?<formBuildId>[^""]+)""")]
	private static partial Regex FormBuildIdRegex();

	/// <summary>
	/// Regex for the addresses from the data.
	/// </summary>
	[GeneratedRegex(@"<option value=""(?<uid>[^""]*)"">(?<address>[^<]+)</option>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the bin days from the data.
	/// </summary>
	[GeneratedRegex(@"<td class=""waste-collection__day--day govuk-table__cell""><time datetime=""(?<date>[^""]+)"">[^<]+</time></td>\s*<td class=""waste-collection__day--type govuk-table__cell"">(?<service>[^<]+)\s*</td>")]
	private static partial Regex BinDayRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting the postcode form
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _binCollectionUrl,
				Method = "GET",
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Prepare client-side request for posting the postcode
		else if (clientSideResponse.RequestId == 1)
		{
			var formBuildId = FormBuildIdRegex().Match(clientSideResponse.Content).Groups["formBuildId"].Value;

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = _binCollectionAjaxUrl,
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "accept", "application/json, text/javascript, */*; q=0.01" },
					{ "content-type", Constants.FormUrlEncoded },
					{ "x-requested-with", Constants.XmlHttpRequest },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{ "postcode", postcode },
					{ "form_build_id", formBuildId },
					{ "form_id", _formId },
				}),
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
			var insertData = GetInsertData(clientSideResponse.Content);
			var rawAddresses = AddressRegex().Matches(insertData)!;

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
		// Prepare client-side request for getting the postcode form
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = _binCollectionUrl,
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for posting the postcode
		else if (clientSideResponse.RequestId == 1)
		{
			var formBuildId = FormBuildIdRegex().Match(clientSideResponse.Content).Groups["formBuildId"].Value;

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = _binCollectionAjaxUrl,
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "accept", "application/json, text/javascript, */*; q=0.01" },
					{ "content-type", Constants.FormUrlEncoded },
					{ "x-requested-with", Constants.XmlHttpRequest },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{ "postcode", address.Postcode! },
					{ "form_build_id", formBuildId },
					{ "form_id", _formId },
				}),
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Prepare client-side request for posting the selected address
		else if (clientSideResponse.RequestId == 2)
		{
			var insertData = GetInsertData(clientSideResponse.Content);
			var formBuildId = FormBuildIdRegex().Match(insertData).Groups["formBuildId"].Value;

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = _binCollectionAjaxUrl,
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "accept", "application/json, text/javascript, */*; q=0.01" },
					{ "content-type", Constants.FormUrlEncoded },
					{ "x-requested-with", Constants.XmlHttpRequest },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{ "uprn", address.Uid! },
					{ "form_build_id", formBuildId },
					{ "form_id", _formId },
				}),
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
			var insertData = GetInsertData(clientSideResponse.Content);
			var rawBinDays = BinDayRegex().Matches(insertData)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["service"].Value.Trim();
				var dateString = rawBinDay.Groups["date"].Value.Trim();

				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				var binDay = new BinDay
				{
					Date = DateUtilities.ParseDateExact(dateString, "yyyy-MM-dd"),
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

	/// <summary>
	/// Gets the inserted HTML content from a Drupal AJAX response.
	/// </summary>
	private static string GetInsertData(string ajaxResponseContent)
	{
		var responseContent = ajaxResponseContent.Trim();

		if (responseContent.StartsWith("<textarea>", StringComparison.Ordinal) && responseContent.EndsWith("</textarea>", StringComparison.Ordinal))
		{
			responseContent = responseContent["<textarea>".Length..^"</textarea>".Length];
		}

		using var jsonDocument = JsonDocument.Parse(responseContent);

		// Iterate through each command, and return inserted HTML content
		foreach (var command in jsonDocument.RootElement.EnumerateArray())
		{
			if (command.GetProperty("command").GetString() == "insert")
			{
				return command.GetProperty("data").GetString()!;
			}
		}

		throw new InvalidOperationException("Invalid Drupal AJAX response.");
	}
}
