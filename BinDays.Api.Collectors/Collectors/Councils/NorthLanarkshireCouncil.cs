namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Collector implementation for North Lanarkshire Council.
/// </summary>
internal sealed partial class NorthLanarkshireCouncil : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "North Lanarkshire Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.northlanarkshire.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "north-lanarkshire";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Grey,
			Keys = [ "General Waste" ],
		},
		new()
		{
			Name = "Paper and Card Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Blue-lidded Recycling Bin" ],
		},
		new()
		{
			Name = "Food and Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Food and Garden" ],
		},
		new()
		{
			Name = "Glass, Metals, Plastics and Cartons Recycling",
			Colour = BinColour.Green,
			Keys = [ "Glass, Metals, Plastics and Cartons" ],
		},
	];

	/// <summary>
	/// The URL of the bin collection dates page.
	/// </summary>
	private const string _binCollectionUrl = "https://www.northlanarkshire.gov.uk/bin-collection-dates";

	/// <summary>
	/// The AJAX URL for the address finder form.
	/// </summary>
	private const string _addressFinderAjaxUrl = "https://www.northlanarkshire.gov.uk/bin-collection-dates?element_parents=address_finder&ajax_form=1&_wrapper_format=drupal_ajax";

	/// <summary>
	/// The Drupal form ID for the address finder form.
	/// </summary>
	private const string _formId = "ace_bin_collection_dates_address_finder_form";

	/// <summary>
	/// The triggering element name for the postcode search button.
	/// </summary>
	private const string _postcodeSearchTrigger = "address_finder_postcode_search_button";

	/// <summary>
	/// Regex for the form build ID from the HTML response.
	/// </summary>
	[GeneratedRegex(@"name=""form_build_id""\s+value=""(?<formBuildId>[^""]+)""")]
	private static partial Regex FormBuildIdRegex();

	/// <summary>
	/// Regex for the addresses from the HTML response.
	/// </summary>
	[GeneratedRegex(@"<option value=""(?<uid>[^""]*)"">(?<address>[^<]+)</option>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the bin type sections from the HTML response.
	/// </summary>
	[GeneratedRegex(@"<div class=""waste-type-container[^""]*"">\s*<div>\s*<h3>(?<service>[^<]+)</h3>[\s\S]*?(?<dates>(?:\s*<p>\d{1,2}\s+[A-Za-z]+\s+\d{4}</p>\s*)+)[\s\S]*?</div>\s*</div>")]
	private static partial Regex BinTypeSectionRegex();

	/// <summary>
	/// Regex for the bin collection dates from the HTML response.
	/// </summary>
	[GeneratedRegex(@"<p>(?<date>\d{1,2}\s+[A-Za-z]+\s+\d{4})</p>")]
	private static partial Regex BinDateRegex();

	/// <summary>
	/// Regex for extracting JSON content from textarea-wrapped Drupal AJAX responses.
	/// </summary>
	[GeneratedRegex(@"^\s*<textarea>(?<content>[\s\S]*)</textarea>\s*$")]
	private static partial Regex DrupalAjaxTextareaRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
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
		else if (clientSideResponse.RequestId == 1)
		{
			var formBuildId = FormBuildIdRegex().Match(clientSideResponse.Content).Groups["formBuildId"].Value;
			var clientSideRequest = CreateAddressSearchRequest(2, postcode, formBuildId);

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		else if (clientSideResponse.RequestId == 2)
		{
			using var jsonDocument = ParseDrupalAjaxResponse(clientSideResponse.Content);

			var insertHtml = string.Empty;

			// Iterate through each command, and find the insert command data
			foreach (var command in jsonDocument.RootElement.EnumerateArray())
			{
				if (command.GetProperty("command").GetString() != "insert")
				{
					continue;
				}

				insertHtml = command.GetProperty("data").GetString()!;
				break;
			}

			var rawAddresses = AddressRegex().Matches(insertHtml)!;

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
		else if (clientSideResponse.RequestId == 1)
		{
			var formBuildId = FormBuildIdRegex().Match(clientSideResponse.Content).Groups["formBuildId"].Value;
			var postcode = address.Postcode!;
			var clientSideRequest = CreateAddressSearchRequest(2, postcode, formBuildId);

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		else if (clientSideResponse.RequestId == 2)
		{
			var formBuildId = ExtractUpdatedFormBuildId(clientSideResponse.Content);
			var postcode = address.Postcode!;
			var selectedAddress = address.Uid!;

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 3,
				Url = _addressFinderAjaxUrl,
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
					{ "x-requested-with", Constants.XmlHttpRequest },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{ "address_finder_postcode_search_text", postcode },
					{ "address_finder[address_select][address_options]", selectedAddress },
					{ "form_build_id", formBuildId },
					{ "form_id", _formId },
					{ "_triggering_element_name", "address_finder_confirm_address_selection" },
					{ "_drupal_ajax", "1" },
				}),
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		else if (clientSideResponse.RequestId == 3)
		{
			var formBuildId = ExtractUpdatedFormBuildId(clientSideResponse.Content);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 4,
				Url = _binCollectionUrl,
				Method = "POST",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
					{ "content-type", Constants.FormUrlEncoded },
				},
				Body = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{ "submit_address_selection", "Next" },
					{ "form_build_id", formBuildId },
					{ "form_id", _formId },
				}),
				Options = new ClientSideOptions
				{
					FollowRedirects = false,
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		else if (clientSideResponse.RequestId == 4)
		{
			var redirectUrl = clientSideResponse.Headers["location"];

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 5,
				Url = redirectUrl,
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		else if (clientSideResponse.RequestId == 5)
		{
			var rawBinTypeSections = BinTypeSectionRegex().Matches(clientSideResponse.Content);

			var binDays = new List<BinDay>();
			foreach (Match rawBinTypeSection in rawBinTypeSections)
			{
				var service = rawBinTypeSection.Groups["service"].Value.Trim();
				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);
				var rawDates = BinDateRegex().Matches(rawBinTypeSection.Groups["dates"].Value);

				foreach (Match rawDate in rawDates)
				{
					var collectionDate = rawDate.Groups["date"].Value.Trim();

					var binDay = new BinDay
					{
						Date = DateUtilities.ParseDateExact(collectionDate, "d MMMM yyyy"),
						Address = address,
						Bins = matchedBins,
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

	/// <summary>
	/// Creates a client-side request for searching addresses by postcode.
	/// </summary>
	private static ClientSideRequest CreateAddressSearchRequest(int requestId, string postcode, string formBuildId)
	{
		var clientSideRequest = new ClientSideRequest
		{
			RequestId = requestId,
			Url = _addressFinderAjaxUrl,
			Method = "POST",
			Headers = new()
			{
				{ "user-agent", Constants.UserAgent },
				{ "content-type", Constants.FormUrlEncoded },
				{ "x-requested-with", Constants.XmlHttpRequest },
			},
			Body = ProcessingUtilities.ConvertDictionaryToFormData(new()
			{
				{ "address_finder_postcode_search_text", postcode },
				{ "form_build_id", formBuildId },
				{ "form_id", _formId },
				{ "_triggering_element_name", _postcodeSearchTrigger },
				{ "_drupal_ajax", "1" },
			}),
		};

		return clientSideRequest;
	}

	/// <summary>
	/// Extracts the updated form build ID from a Drupal AJAX response.
	/// </summary>
	private static string ExtractUpdatedFormBuildId(string content)
	{
		using var jsonDocument = ParseDrupalAjaxResponse(content);

		// Iterate through each command, and find the updated form build ID
		foreach (var command in jsonDocument.RootElement.EnumerateArray())
		{
			if (command.GetProperty("command").GetString() != "update_build_id")
			{
				continue;
			}

			return command.GetProperty("new").GetString()!;
		}

		throw new InvalidOperationException("No update_build_id command found in Drupal AJAX response.");
	}

	/// <summary>
	/// Parses a Drupal AJAX response into a JSON document.
	/// </summary>
	private static JsonDocument ParseDrupalAjaxResponse(string content)
	{
		var normalizedContent = content;
		var textareaMatch = DrupalAjaxTextareaRegex().Match(content);

		if (textareaMatch.Success)
		{
			normalizedContent = System.Net.WebUtility.HtmlDecode(textareaMatch.Groups["content"].Value);
		}

		return JsonDocument.Parse(normalizedContent);
	}
}
