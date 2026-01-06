namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors.Vendors;
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Text.Json;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Collector implementation for Torbay Council.
	/// </summary>
	internal sealed partial class TorbayCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "Torbay Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.torbay.gov.uk/recycling/bin-collections/");

		/// <inheritdoc/>
		public override string GovUkId => "torbay";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly IReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "General Waste",
				Colour = BinColour.Grey,
				Type = BinType.Bin,
				Keys = new List<string>() { "Domestic", "General", "Refuse" }.AsReadOnly(),
			},
			new()
			{
				Name = "Recycling",
				Colour = BinColour.Green,
				Type = BinType.Box,
				Keys = new List<string>() { "Recycling" }.AsReadOnly(),
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Brown,
				Type = BinType.Bin,
				Keys = new List<string>() { "Garden" }.AsReadOnly(),
			},
			new()
			{
				Name = "Food Waste",
				Colour = BinColour.Brown,
				Type = BinType.Caddy,
				Keys = new List<string>() { "Food" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <summary>
		/// Regex for the __RequestVerificationToken.
		/// </summary>
		[GeneratedRegex(@"<input[^>]*name=""__RequestVerificationToken""[^>]*value=""(?<token>[^""]+)""", RegexOptions.IgnoreCase)]
		private static partial Regex RequestVerificationTokenRegex();

		/// <summary>
		/// Regex for the FormGuid value.
		/// </summary>
		[GeneratedRegex(@"<input[^>]*name=""FormGuid""[^>]*value=""(?<formGuid>[^""]+)""", RegexOptions.IgnoreCase)]
		private static partial Regex FormGuidRegex();

		/// <summary>
		/// Regex for the ObjectTemplateID value.
		/// </summary>
		[GeneratedRegex(@"<input[^>]*name=""ObjectTemplateID""[^>]*value=""(?<objectTemplateId>[^""]+)""", RegexOptions.IgnoreCase)]
		private static partial Regex ObjectTemplateIdRegex();

		/// <summary>
		/// Regex for bin day rows.
		/// </summary>
		[GeneratedRegex(@"resirow[^>]*>\s*<div[^>]*class=""col[^""]*""[^>]*>.*?<div[^>]*class=""col""[^>]*>(?<date>[^<]+)</div>\s*<div[^>]*class=""col""[^>]*>(?<service>[^<]+)</div", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
		private static partial Regex BinDayRegex();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			var formattedPostcode = ProcessingUtilities.FormatPostcode(postcode);

			// Prepare client-side request for getting form tokens and cookies
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = "https://selfservice-torbay.servicebuilder.co.uk/renderform?t=62&k=09B72FF904A21A4B01A72AB6CCF28DC95105031C",
					Method = "GET",
					Headers = new()
					{
						{ "User-Agent", Constants.UserAgent },
					},
				};

				return new GetAddressesResponse
				{
					NextClientSideRequest = clientSideRequest
				};
			}
			// Prepare client-side request for getting addresses
			else if (clientSideResponse.RequestId == 1)
			{
				var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);

				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{ "query", formattedPostcode },
					{ "searchNlpg", "False" },
					{ "classification", string.Empty },
				});

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 2,
					Url = "https://selfservice-torbay.servicebuilder.co.uk/core/addresslookup",
					Method = "POST",
					Headers = new()
					{
						{ "User-Agent", Constants.UserAgent },
						{ "Content-Type", "application/x-www-form-urlencoded" },
						{ "Cookie", requestCookies },
						{ "x-requested-with", "XMLHttpRequest" },
					},
					Body = requestBody,
				};

				return new GetAddressesResponse
				{
					NextClientSideRequest = clientSideRequest
				};
			}
			// Process addresses from response
			else if (clientSideResponse.RequestId == 2)
			{
				using var document = JsonDocument.Parse(clientSideResponse.Content);

				var addresses = new List<Address>();

				foreach (var element in document.RootElement.EnumerateArray())
				{
					var uid = element.GetProperty("Key").GetString();
					var value = element.GetProperty("Value").GetString();

					var address = new Address
					{
						Property = value?.Trim(),
						Postcode = formattedPostcode,
						Uid = uid,
					};

					addresses.Add(address);
				}

				return new GetAddressesResponse
				{
					Addresses = addresses.OrderBy(address => address.Property).ToList().AsReadOnly(),
				};
			}

			throw new InvalidOperationException("Invalid client-side request.");
		}

		/// <inheritdoc/>
		public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
		{
			var formattedPostcode = ProcessingUtilities.FormatPostcode(address.Postcode ?? string.Empty);

			// Prepare client-side request for getting form tokens and cookies
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = "https://selfservice-torbay.servicebuilder.co.uk/renderform?t=62&k=09B72FF904A21A4B01A72AB6CCF28DC95105031C",
					Method = "GET",
					Headers = new()
					{
						{ "User-Agent", Constants.UserAgent },
					},
				};

				return new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest
				};
			}
			// Prepare client-side request for getting bin days
			else if (clientSideResponse.RequestId == 1)
			{
				var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);

				var token = RequestVerificationTokenRegex().Match(clientSideResponse.Content).Groups["token"].Value;
				var formGuid = FormGuidRegex().Match(clientSideResponse.Content).Groups["formGuid"].Value;
				var objectTemplateId = ObjectTemplateIdRegex().Match(clientSideResponse.Content).Groups["objectTemplateId"].Value;

				var requestBody = ProcessingUtilities.ConvertDictionaryToFormData(new()
				{
					{ "__RequestVerificationToken", token },
					{ "FormGuid", formGuid },
					{ "ObjectTemplateID", objectTemplateId },
					{ "Trigger", "submit" },
					{ "CurrentSectionID", "0" },
					{ "TriggerCtl", string.Empty },
					{ "FF1168", address.Uid ?? string.Empty },
					{ "FF1168lbltxt", "Please select your address" },
					{ "FF1168-text", formattedPostcode },
				});

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 2,
					Url = "https://selfservice-torbay.servicebuilder.co.uk/renderform/Form",
					Method = "POST",
					Headers = new()
					{
						{ "User-Agent", Constants.UserAgent },
						{ "Content-Type", "application/x-www-form-urlencoded" },
						{ "Cookie", requestCookies },
						{ "x-requested-with", "XMLHttpRequest" },
					},
					Body = requestBody,
				};

				return new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest
				};
			}
			// Process bin days from response
			else if (clientSideResponse.RequestId == 2)
			{
				var binDays = new List<BinDay>();

				foreach (Match match in BinDayRegex().Matches(clientSideResponse.Content))
				{
					var dateString = match.Groups["date"].Value.Trim();
					var service = match.Groups["service"].Value.Trim();

					var date = DateOnly.ParseExact(
						dateString,
						"dddd dd MMMM yyyy",
						CultureInfo.InvariantCulture
					);

					var bins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

					var binDay = new BinDay
					{
						Date = date,
						Address = address,
						Bins = bins,
					};

					binDays.Add(binDay);
				}

				return new GetBinDaysResponse
				{
					BinDays = ProcessingUtilities.ProcessBinDays(binDays),
				};
			}

			throw new InvalidOperationException("Invalid client-side request.");
		}
	}
}
