using System.Text.RegularExpressions;

namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Globalization;
	using System.Text.Json;

	/// <summary>
	/// Collector implementation for North Hertfordshire Council.
	/// </summary>
	internal sealed partial class NorthHertfordshireCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "North Hertfordshire Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.north-herts.gov.uk/find-your-bin-collection-day");

		/// <inheritdoc/>
		public override string GovUkId => "north-hertfordshire";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new() { Name = "Non-Recyclable Waste", Colour = BinColour.Purple, Keys = new List<string>() { "Non-Recyclable Waste" }.AsReadOnly(), },
			new() { Name = "Mixed Recycling", Colour = BinColour.Black, Keys = new List<string>() { "Recycling" }.AsReadOnly(), },
			new() { Name = "Cardboard & Paper", Colour = BinColour.Blue, Keys = new List<string>() { "Cardboard" }.AsReadOnly(), },
			new()
			{
				Name = "Food Waste",
				Colour = BinColour.Brown,
				Keys = new List<string>() { "Food waste" }.AsReadOnly(),
				Type = BinType.Caddy
			},
			new()
			{
				Name = "Garden Waste", Colour = BinColour.Brown, Keys = new List<string>() { "Garden Waste" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <summary>
		/// Regex for the CSRF token
		/// </summary>
		[GeneratedRegex(@"CSRF\s=\s'(?<token>[^']+)")]
		private static partial Regex CsrfTokenRegex();

		/// <summary>
		/// Regex for the AJAX URL
		/// </summary>
		[GeneratedRegex(@"""AJAX_URL"":""(?<url>.*?)""")]
		private static partial Regex AjaxUrlRegex();

		/// <summary>
		/// Regex for the AJAX Dynamic URL
		/// </summary>
		[GeneratedRegex(@"""AJAX_DYNAMIC_URL"":""(?<url>.*?)""")]
		private static partial Regex AjaxDynamicUrlRegex();

		/// <summary>
		/// Regex for the Levels Token
		/// </summary>
		[GeneratedRegex(@"""levels"":""(?<token>.*?)""")]
		private static partial Regex LevelsTokenRegex();

		/// <summary>
		/// Regex for the Levels Token
		/// </summary>
		[GeneratedRegex(@"name=""submission_token""\s+value=""(?<token>.*?)""")]
		private static partial Regex SubmissionTokenRegex();

		/// <summary>
		/// Regex for the address elements.
		/// </summary>
		[GeneratedRegex(@"data-id=""(?<id>\d+)""[\s\S]*?aria-label=""(?<address>[^""]+)""")]
		private static partial Regex AddressRegex();

		/// <summary>
		/// Regex for the bin days.
		/// </summary>
		[GeneratedRegex(@"sans-serif;"">(?<service>[^<]+)[\s\S]+?N[\s\S]+?br>(?<date>[^<]+)")]
		private static partial Regex BinDaysRegex();

		/// <summary>
		/// Regex for removing the st|nd|rd|th from the date part
		/// </summary>
		[GeneratedRegex(@"(?<=\d)(st|nd|rd|th)")]
		private static partial Regex CollectionDateRegex();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting addresses
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = "https://waste.nc.north-herts.gov.uk/w/webpage/find-bin-collection-day-input-address",
					Method = "GET",
					Headers = [],
					Body = null,
				};

				var getAddressesResponse = new GetAddressesResponse()
				{
					Addresses = null,
					NextClientSideRequest = clientSideRequest
				};

				return getAddressesResponse;
			}

			if (clientSideResponse.RequestId == 1)
			{
				var cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
					clientSideResponse.Headers["set-cookie"]);
				var csrfToken = CsrfTokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = "https://waste.nc.north-herts.gov.uk/w/webpage/find-bin-collection-day-input-address",
					Method = "POST",
					Headers = new Dictionary<string, string>()
					{
						{ "user-agent", Constants.UserAgent },
						{ "content-type", "application/x-www-form-urlencoded" },
						{ "cookie", cookie },
						{"x-requested-with", "XMLHttpRequest"},
						{"accept", "application/json, text/javascript, */*; q=0.01"}
					},
					Body = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
					{
						{ "_dummy", "1" },
						{ "_session_storage", "{\"_global\":{}}" },
						{ "_update_page_content_request", "1" },
						{ "form_check_ajax", csrfToken },
					}),
					Options = new ClientSideOptions
					{
						Metadata = { { "cookie", cookie }, { "form_check_ajax", csrfToken } }
					},
				};

				return new GetAddressesResponse() { Addresses = null, NextClientSideRequest = clientSideRequest };
			}

			if (clientSideResponse.RequestId == 2)
			{
				var cleanedContent = clientSideResponse.Content.Replace("\\", "").Replace("&quot;", "\"");
				var nextUrl = AjaxUrlRegex().Match(cleanedContent).Groups["url"].Value;
				var submissionToken = SubmissionTokenRegex().Match(cleanedContent).Groups["token"].Value;
				var levelsToken = LevelsTokenRegex().Match(cleanedContent).Groups["token"].Value;

				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
				{
					{ "levels", levelsToken },
					{ "search_string", postcode },
					{ "display_limit", "75" },
					{ "presenter_settings[records_limit]", "75" },
					{ "presenter_settings[load_more_records_label]", "Click here to load more addresses" },
					{ "presenter_settings[min_characters]", "3" },
					{ "presenter_settings[exact_match_first]", "0" },
					{ "settings[message_offline_mode_unique_lookup]", "" },
					{ "settings[wrapper]", "16" },
					{ "settings[presenter]", "73" },
					{ "settings[remember_last_value]", "0" },
					{
						"settings[label]",
						"Search for an address. For example, 123 Test Road, or SG6 3JF. Postcodes must contain a space."
					},
					{ "settings[hint]", "" },
					{ "settings[required]", "1" },
					{ "settings[omit_if_blank]", "0" },
					{ "settings[title_field_path]", ":PRO0000228GBLLP1" },
					{ "settings[lookup_field_path]", ":CMP0000243GBNLM1" },
					{ "settings[lookup_comparator]", "17" },
					{ "settings[order_field_path]", ":CMP0000211GBNLM1" },
					{ "settings[order_direction]", "ASC" },
					{ "settings[force_to_default_input_value]", "0" },
					{ "settings[logged_in_user_visibility_relationship][OBJ0000063GBNLM9_inverse]", "0" },
					{ "settings[limit_subset_field_path]", ":SUB0000445GBNLM1" },
					{ "settings[allow_existing_invalid_value]", "0" },
					{ "settings[show_value_history]", "0" },
					{ "settings[presenter_settings][records_limit]", "75" },
					{
						"settings[presenter_settings][load_more_records_label]", "Click here to load more addresses"
					},
					{ "settings[presenter_settings][min_characters]", "3" },
					{ "settings[presenter_settings][exact_match_first]", "0" },
					{ "settings[display_on_small_devices]", "true" },
					{ "settings[display_on_medium_devices]", "true" },
					{ "settings[display_on_large_devices]", "true" },
					{ "settings[tiles_orientation]", "horizontal" },
					{ "settings[message_offline_mode_callback]", "" },
					{ "context_page_id", "PAG0000732GBNLM1" },
					{ "form_check_ajax", clientSideResponse.Options.Metadata["form_check_ajax"] },
				});


				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 3,
					Url = "https://waste.nc.north-herts.gov.uk" + nextUrl + $"&ajax_action=html_get_type_ahead_results",
					Method = "POST",
					Headers =
						new Dictionary<string, string>()
						{
							{ "user-agent", Constants.UserAgent },
							{ "content-type", "application/x-www-form-urlencoded" },
							{ "cookie", clientSideResponse.Options.Metadata["cookie"] },
						},
					Body = requestBody,
					Options = clientSideResponse.Options,
				};

				return new GetAddressesResponse() { Addresses = null, NextClientSideRequest = clientSideRequest };
			}

			// Process addresses from response
			if (clientSideResponse.RequestId == 3)
			{
				// Get addresses from response
				var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

				// Iterate through each address, and create a new address object
				var addresses = new List<Address>();
				foreach (Match rawAddress in rawAddresses)
				{
					var address = new Address()
					{
						Property = rawAddress.Groups["address"].Value,
						Street = string.Empty,
						Town = string.Empty,
						Postcode = postcode,
						Uid = rawAddress.Groups["id"].Value,
					};
					addresses.Add(address);
				}

				var getAddressesResponse = new GetAddressesResponse()
				{
					Addresses = addresses.AsReadOnly(),
					NextClientSideRequest = null
				};

				return getAddressesResponse;
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}

		/// <inheritdoc/>
		public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
		{
			// The first 2 requests are the same as for GetAddresses, as we need the same variables
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = "https://waste.nc.north-herts.gov.uk/w/webpage/find-bin-collection-day-input-address",
					Method = "GET",
					Headers = [],
					Body = null,
				};

				return new GetBinDaysResponse()
				{
					BinDays = null,
					NextClientSideRequest = clientSideRequest
				};
			}

			if (clientSideResponse.RequestId == 1)
			{
				var cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
					clientSideResponse.Headers["set-cookie"]);
				var csrfToken = CsrfTokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = "https://waste.nc.north-herts.gov.uk/w/webpage/find-bin-collection-day-input-address",
					Method = "POST",
					Headers = new Dictionary<string, string>()
					{
						{ "user-agent", Constants.UserAgent },
						{ "content-type", "application/x-www-form-urlencoded" },
						{ "cookie", cookie },
						{ "x-requested-with", "XMLHttpRequest" },
						{ "accept", "application/json, text/javascript, */*; q=0.01" }
					},
					Body = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
					{
						{ "_dummy", "1" },
						{ "_session_storage", "{\"_global\":{}}" },
						{ "_update_page_content_request", "1" },
						{ "form_check_ajax", csrfToken },
					}),
					Options = new ClientSideOptions
					{
						Metadata = { { "cookie", cookie }, { "form_check_ajax", csrfToken } }
					},
				};

				return new GetBinDaysResponse()
				{
					BinDays = null,
					NextClientSideRequest = clientSideRequest
				};
			}

			// Now we're on to new requests...
			if (clientSideResponse.RequestId == 2)
			{
				var cleanedContent = clientSideResponse.Content.Replace("\\", "").Replace("&quot;", "\"");
				var nextUrl = AjaxUrlRegex().Match(cleanedContent).Groups["url"].Value;
				var submissionToken = SubmissionTokenRegex().Match(cleanedContent).Groups["token"].Value;
				var levelsToken = LevelsTokenRegex().Match(cleanedContent).Groups["token"].Value;
				var dynamicUrl = AjaxDynamicUrlRegex().Match(cleanedContent).Groups["url"].Value;
				// TODO wrong URL extracted for this step. It needs to be the one like:
				//   https://waste.nc.north-herts.gov.uk/w/webpage/find-bin-collection-day-input-address?webpage_subpage_id=PAG0000732GBNLM1&webpage_token=9bc074e8acd954409e1f654670f56d3f27ce1ea5382bfe84c56ad1c19204477f
				var formData = new Dictionary<string, string>()
				{
					{"form_check", clientSideResponse.Options.Metadata["form_check_ajax"]},
					{"submitted_page_id", "PAG0000732GBNLM1"},
					{"submitted_widget_group_id", "PWG0003519GBNLM1"},
					{"submitted_widget_group_type", "modify"},
					{"submission_token", submissionToken},
					{"payload[PAG0000732GBNLM1][PWG0003519GBNLM1][PCL0006492GBNLM1][formtable][C_68e80da9e52ee][PCF0021202GBNLM1]", address.Uid!},
					{"payload[PAG0000732GBNLM1][PWG0003519GBNLM1][PCL0006492GBNLM1][formtable][C_68e80da9e52ee][PCF0021201GBNLM1]", "Select address and continue"},
					{"submit_fragment_id", "PCF0021201GBNLM1"},
					{"_session_storage", "{\"_global\":{\"destination_stack\":[\"w/webpage/find-bin-collection-day-input-address\"]}}"},
					{"_update_page_content_request", "1"},
					{"form_check_ajax", clientSideResponse.Options.Metadata["form_check_ajax"]},
				};
				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(formData);
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 3,
					Url = "https://waste.nc.north-herts.gov.uk" + dynamicUrl,
					Method = "POST",
					Headers = new Dictionary<string, string>()
					{
						{ "user-agent", Constants.UserAgent },
						{ "content-type", "application/x-www-form-urlencoded" },
						{ "cookie", clientSideResponse.Options.Metadata["cookie"] },
						{"x-requested-with", "XMLHttpRequest"},
						{"accept", "application/json, text/javascript, */*; q=0.01"}
					},
					Body = requestBody,
					Options = clientSideResponse.Options,
				};

				return new GetBinDaysResponse()
				{
					BinDays = null,
					NextClientSideRequest = clientSideRequest
				};
			}

			if (clientSideResponse.RequestId == 3)
			{
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
				var formData = new Dictionary<string, string>()
				{
					{"_dummy", "1"},
					{"_session_storage", "{\"_global\":{\"destination_stack\":[\"w/webpage/find-bin-collection-day-input-address\"]}}"},
					{"_update_page_content_request", "1"},
					{"form_check_ajax", clientSideResponse.Options.Metadata["form_check_ajax"]},
				};
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 4,
					Url = "https://waste.nc.north-herts.gov.uk" + jsonDoc.RootElement.GetProperty("redirect_url").GetString()!,
					Method = "POST",
					Headers = new Dictionary<string, string>()
					{
						{ "user-agent", Constants.UserAgent },
						{ "content-type", "application/x-www-form-urlencoded" },
						{ "cookie", clientSideResponse.Options.Metadata["cookie"] },
						{"x-requested-with", "XMLHttpRequest"},
						{"accept", "application/json, text/javascript, */*; q=0.01"}
					},
					Body = ProcessingUtilities.ConvertDictionaryToFormData(formData),
				};

				return new GetBinDaysResponse()
				{
					BinDays = null,
					NextClientSideRequest = clientSideRequest
				};
			}

			// Process bin days from response
			if (clientSideResponse.RequestId == 4)
			{
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
				var htmlToParse = jsonDoc.RootElement.GetProperty("data").GetString()!;
				var rawBinDays = BinDaysRegex().Matches(htmlToParse);
				// Example: Tuesday 10 August 2021
				var format = "dddd d MMMM yyyy";

				// Iterate through each bin day, and create a new bin day object
				var binDays = new List<BinDay>();
				foreach (Match rawBinDay in rawBinDays)
				{
					var service = rawBinDay.Groups["service"].Value;
					var collectionDate = CollectionDateRegex()
						.Replace(rawBinDay.Groups["date"].Value, "");

					var date = DateOnly.ParseExact(collectionDate, format, CultureInfo.InvariantCulture);

					// Get matching bin types from the service using the keys
					var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

					var binDay = new BinDay()
					{
						Date = date,
						Address = address,
						Bins = matchedBinTypes,
					};

					binDays.Add(binDay);
				}

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = ProcessingUtilities.ProcessBinDays(binDays),
					NextClientSideRequest = null
				};

				return getBinDaysResponse;
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}
	}
}
