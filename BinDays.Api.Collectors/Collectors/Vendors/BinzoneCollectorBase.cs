namespace BinDays.Api.Collectors.Collectors.Vendors
{
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Base collector implementation for councils using the Ebase "Binzone" desktop form
	/// (specifically the South Oxfordshire and Vale of White Horse shared service).
	/// </summary>
	internal abstract partial class BinzoneCollectorBase : GovUkCollectorBase
	{
		/// <summary>
		/// The base URL for the eform system (e.g. "https://eform.southoxon.gov.uk").
		/// </summary>
		protected abstract string EformBaseUrl { get; }

		/// <summary>
		/// The service identifier used in the SOVA_TAG query parameter (e.g. "SOUTH" or "VALE").
		/// </summary>
		protected abstract string ServiceId { get; }

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly IReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Rubbish",
				Colour = BinColour.Black,
				Keys = new List<string>() { "grey bin" }.AsReadOnly(),
			},
			new()
			{
				Name = "Recycling",
				Colour = BinColour.Green,
				Keys = new List<string>() { "green bin" }.AsReadOnly(),
			},
			new()
			{
				Name = "Food Waste",
				Colour = BinColour.Green,
				Keys = new List<string>() { "food bin" }.AsReadOnly(),
				Type = BinType.Caddy,
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Brown,
				Keys = new List<string>() { "garden waste bin" }.AsReadOnly(),
			},
			new()
			{
				Name = "Small Electrical Items",
				Colour = BinColour.Grey,
				Keys = new List<string>() { "small electrical items" }.AsReadOnly(),
				Type = BinType.Bag,
			},
			new()
			{
				Name = "Textiles",
				Colour = BinColour.Grey,
				Keys = new List<string>() { "textiles" }.AsReadOnly(),
				Type = BinType.Bag,
			},
		}.AsReadOnly();

		/// <summary>
		/// Regex for the ebz/ebs token value from a URL.
		/// </summary>
		[GeneratedRegex("ebz=([^&]+)")]
		private static partial Regex EbzRegex();

		/// <summary>
		/// Regex for extracting addresses and their corresponding control IDs from the HTML table.
		/// </summary>
		[GeneratedRegex(@"(?s)class=""[^""]*eb-58-fieldHyperlink[^""]*""[^>]*>\s*(?<address>[^<]+)\s*</a>.*?name=""(?<uid>CTRL:63:_:D:\d+)""")]
		private static partial Regex AddressRegex();

		/// <summary>
		/// Regex for extracting the HID:inputs value from the form.
		/// </summary>
		[GeneratedRegex(@"name=""HID:inputs"" value=""(?<value>[^""]+)""")]
		private static partial Regex HidInputsRegex();

		/// <summary>
		/// Regex for extracting date and bin information from the bin details slider.
		/// Uses [\s\S]+? to match across newlines and include HTML tags (like anchors) within the description.
		/// </summary>
		[GeneratedRegex(@"class=""binextra"">\s*(?<date>[^<]+?)\s*-<br>\s*(?<bins>[\s\S]+?)<br>")]
		private static partial Regex BinDetailsRegex();

		/// <summary>
		/// Regex for removing HTML tags from the bin description.
		/// </summary>
		[GeneratedRegex("<.*?>")]
		private static partial Regex HtmlTagRegex();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting the initial session redirect
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = $"{EformBaseUrl}/ebase/ufsmain?formid=BINZONE_DESKTOP&SOVA_TAG={ServiceId}",
					Method = "GET",
					Options = new ClientSideOptions()
					{
						// We need to trap the 302 to get the Location header
						FollowRedirects = false,
					},
				};

				return new GetAddressesResponse { NextClientSideRequest = clientSideRequest };
			}
			// Prepare client-side request for initializing the session (GET)
			else if (clientSideResponse.RequestId == 1)
			{
				var cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
					clientSideResponse.Headers["set-cookie"]
				);

				var relativeLocation = clientSideResponse.Headers["location"];
				var fullRedirectUrl = $"{EformBaseUrl}/ebase/{relativeLocation}";

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = fullRedirectUrl,
					Method = "GET",
					Headers = new Dictionary<string, string>
					{
						{ "cookie", cookie },
					},
					Options = new ClientSideOptions
					{
						Metadata =
						{
							{ "cookie", cookie },
							{ "referer", fullRedirectUrl },
						}
					}
				};

				return new GetAddressesResponse { NextClientSideRequest = clientSideRequest };
			}
			// Prepare client-side request for performing the search (POST)
			else if (clientSideResponse.RequestId == 2)
			{
				var cookie = clientSideResponse.Options.Metadata["cookie"];
				var refererUrl = clientSideResponse.Options.Metadata["referer"];
				var ebs = EbzRegex().Match(refererUrl).Groups[1].Value;

				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
				{
					{ "formid", "/Forms/BINZONE_DESKTOP" },
					{ "ebs", ebs },
					{ "formstack", "BINZONE_DESKTOP:f267e852-5fff-456e-96d7-83cd429c5109" },
					{ "pageSeq", "1" },
					{ "pageId", "WHERE_DO_YOU_LIVE" },
					{ "formStateId", "1" },
					{ "CTRL:2:_:A", postcode },
					{ "CTRL:20:_", "Search" },
					{ "HID:inputs", "ICTRL:2:_:A,ACTRL:20:_,ACTRL:24:_,ICTRL:70:_:A,ICTRL:31:_:A,ICTRL:32:_:A,APAGE:E.h,APAGE:B.h,APAGE:N.h,APAGE:S.h,APAGE:R.h" },
				});

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 3,
					Url = $"{EformBaseUrl}/ebase/BINZONE_DESKTOP.eb?ebz={ebs}",
					Method = "POST",
					Body = requestBody,
					Headers = new Dictionary<string, string>
					{
						{ "cookie", cookie },
						{ "Content-Type", "application/x-www-form-urlencoded" },
					},
				};

				return new GetAddressesResponse { NextClientSideRequest = clientSideRequest };
			}
			// Process addresses from response
			else if (clientSideResponse.RequestId == 3)
			{
				var addresses = new List<Address>();
				var matches = AddressRegex().Matches(clientSideResponse.Content);

				foreach (Match match in matches)
				{
					var uid = match.Groups["uid"].Value;
					var addressText = match.Groups["address"].Value.Trim();

					addresses.Add(new Address()
					{
						Property = addressText,
						Postcode = postcode,
						Uid = uid,
					});
				}

				return new GetAddressesResponse { Addresses = addresses.AsReadOnly() };
			}

			throw new InvalidOperationException("Invalid client-side request.");
		}

		/// <inheritdoc/>
		public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting the initial session redirect
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 1,
					Url = $"{EformBaseUrl}/ebase/ufsmain?formid=BINZONE_DESKTOP&SOVA_TAG={ServiceId}",
					Method = "GET",
					Options = new ClientSideOptions()
					{
						FollowRedirects = false,
					},
				};

				return new GetBinDaysResponse { NextClientSideRequest = clientSideRequest };
			}
			// Prepare client-side request for initializing the session (GET)
			else if (clientSideResponse.RequestId == 1)
			{
				var cookie = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(
					clientSideResponse.Headers["set-cookie"]
				);

				var relativeLocation = clientSideResponse.Headers["location"];
				var fullRedirectUrl = $"{EformBaseUrl}/ebase/{relativeLocation}";

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 2,
					Url = fullRedirectUrl,
					Method = "GET",
					Headers = new Dictionary<string, string>
					{
						{ "cookie", cookie },
					},
					Options = new ClientSideOptions
					{
						Metadata =
						{
							{ "cookie", cookie },
							{ "referer", fullRedirectUrl },
						}
					}
				};

				return new GetBinDaysResponse { NextClientSideRequest = clientSideRequest };
			}
			// Prepare client-side request for performing the search (POST)
			else if (clientSideResponse.RequestId == 2)
			{
				var cookie = clientSideResponse.Options.Metadata["cookie"];
				var refererUrl = clientSideResponse.Options.Metadata["referer"];
				var ebs = EbzRegex().Match(refererUrl).Groups[1].Value;

				// Save EBS for next step
				clientSideResponse.Options.Metadata.Add("ebs", ebs);

				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
				{
					{ "formid", "/Forms/BINZONE_DESKTOP" },
					{ "ebs", ebs },
					{ "formstack", "BINZONE_DESKTOP:f267e852-5fff-456e-96d7-83cd429c5109" },
					{ "pageSeq", "1" },
					{ "pageId", "WHERE_DO_YOU_LIVE" },
					{ "formStateId", "1" },
					{ "CTRL:2:_:A", address.Postcode! },
					{ "CTRL:20:_", "Search" },
					{ "HID:inputs", "ICTRL:2:_:A,ACTRL:20:_,ACTRL:24:_,ICTRL:70:_:A,ICTRL:31:_:A,ICTRL:32:_:A,APAGE:E.h,APAGE:B.h,APAGE:N.h,APAGE:S.h,APAGE:R.h" },
				});

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 3,
					Url = $"{EformBaseUrl}/ebase/BINZONE_DESKTOP.eb?ebz={ebs}",
					Method = "POST",
					Body = requestBody,
					Headers = new Dictionary<string, string>
					{
						{ "cookie", cookie },
						{ "Content-Type", "application/x-www-form-urlencoded" },
					},
					Options = clientSideResponse.Options,
				};

				return new GetBinDaysResponse { NextClientSideRequest = clientSideRequest };
			}
			// Prepare client-side request to select the address (POST)
			else if (clientSideResponse.RequestId == 3)
			{
				var cookie = clientSideResponse.Options.Metadata["cookie"];
				var ebs = clientSideResponse.Options.Metadata["ebs"];

				// Scrape the dynamic HID:inputs value from the response
				var hidInputs = HidInputsRegex().Match(clientSideResponse.Content).Groups["value"].Value;

				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new Dictionary<string, string>()
				{
					{ "formid", "/Forms/BINZONE_DESKTOP" },
					{ "ebs", ebs },
					{ "formstack", "BINZONE_DESKTOP:f267e852-5fff-456e-96d7-83cd429c5109" },
					{ "pageSeq", "2" },
					{ "pageId", "_ADDRESS" },
					{ "formStateId", "1" },
					{ $"{address.Uid}.x", "10" },
					{ $"{address.Uid}.y", "10" },
					{ "HID:inputs", hidInputs },
				});

				var clientSideRequest = new ClientSideRequest()
				{
					RequestId = 4,
					Url = $"{EformBaseUrl}/ebase/BINZONE_DESKTOP.eb?ebz={ebs}",
					Method = "POST",
					Body = requestBody,
					Headers = new Dictionary<string, string>
					{
						{ "cookie", cookie },
						{ "Content-Type", "application/x-www-form-urlencoded" },
					},
				};

				return new GetBinDaysResponse { NextClientSideRequest = clientSideRequest };
			}
			// Process bin days from response
			else if (clientSideResponse.RequestId == 4)
			{
				var matches = BinDetailsRegex().Matches(clientSideResponse.Content);
				var binDays = new List<BinDay>();

				foreach (Match match in matches)
				{
					var dateString = match.Groups["date"].Value.Trim();
					var binString = match.Groups["bins"].Value;

					// Remove HTML tags (e.g. hyperlinks for garden waste)
					binString = HtmlTagRegex().Replace(binString, "");

					// Parse the date (e.g. "Thursday 27 November")
					var date = DateOnly.ParseExact(
						dateString,
						"dddd d MMMM",
						CultureInfo.InvariantCulture
					);

					// If the parsed date is in a month that has already passed this year, assume it's for next year
					if (date.Month < DateTime.Now.Month)
					{
						date = date.AddYears(1);
					}

					// Split bin description by comma or "and" to get individual bins
					var bins = binString.Split([",", " and "], StringSplitOptions.RemoveEmptyEntries);

					foreach (var binName in bins)
					{
						var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, binName);

						var binDay = new BinDay()
						{
							Date = date,
							Address = address,
							Bins = matchedBins,
						};
						binDays.Add(binDay);
					}
				}

				return new GetBinDaysResponse { BinDays = ProcessingUtilities.ProcessBinDays(binDays) };
			}

			throw new InvalidOperationException("Invalid client-side request.");
		}
	}
}
