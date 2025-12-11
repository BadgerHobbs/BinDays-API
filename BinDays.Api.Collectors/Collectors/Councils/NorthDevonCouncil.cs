namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors.Vendors;
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text.Json;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Collector implementation for North Devon Council.
	/// </summary>
	internal sealed partial class NorthDevonCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "North Devon Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.northdevon.gov.uk/bins-and-recycling/collection-dates/");

		/// <inheritdoc/>
		public override string GovUkId => "north-devon";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly IReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "General Waste",
				Colour = BinColour.Black,
				Keys = new List<string>() { "Black Bin", "Black Bin/Bag", "Waste-Black" }.AsReadOnly(),
				Type = BinType.Bin,
			},
			new()
			{
				Name = "Recycling (Glass)",
				Colour = BinColour.Blue,
				Keys = new List<string>() { "Blue Box" }.AsReadOnly(),
				Type = BinType.Box,
			},
			new()
			{
				Name = "Recycling (Plastics & Tins)",
				Colour = BinColour.Black,
				Keys = new List<string>() { "Black Box", "Green Box" }.AsReadOnly(),
				Type = BinType.Box,
			},
			new()
			{
				Name = "Recycling (Paper)",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Green Bag" }.AsReadOnly(),
				Type = BinType.Bag,
			},
			new()
			{
				Name = "Recycling (Cardboard)",
				Colour = BinColour.Brown,
				Keys = new List<string>() { "Brown Bag" }.AsReadOnly(),
				Type = BinType.Bag,
			},
			new()
			{
				Name = "Food Waste",
				Colour = BinColour.Brown,
				Keys = new List<string>() { "Kerbside Caddy", "Caddy" }.AsReadOnly(),
				Type = BinType.Caddy,
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Garden", "Green Wheelie Bin" }.AsReadOnly(),
				Type = BinType.Bin,
			},
		}.AsReadOnly();

		/// <summary>
		/// Cookie value to pin the product variant.
		/// </summary>
		private const string ProductCookie = "product=SELF";

		/// <summary>
		/// Regex to extract the collection date from a service detail.
		/// </summary>
		[GeneratedRegex(@"(?<Date>\d{2}/\d{2}/\d{4})")]
		private static partial Regex ServiceDetailDateRegex();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Step 1: Get auth session token (sid)
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = "https://my.northdevon.gov.uk/authapi/isauthenticated?uri=https%253A%252F%252Fmy.northdevon.gov.uk%252Fservice%252FWasteRecyclingCollectionCalendar&hostname=my.northdevon.gov.uk&withCredentials=true",
					Method = "GET",
					Headers = new Dictionary<string, string>
					{
						{ "cookie", ProductCookie },
					},
				};

				return new GetAddressesResponse
				{
					NextClientSideRequest = clientSideRequest
				};
			}

			// Step 2: Lookup addresses using the sid
			else if (clientSideResponse.RequestId == 1)
			{
				// Include session and product cookies for subsequent lookups
				using var sidDoc = JsonDocument.Parse(clientSideResponse.Content);
				var sid = sidDoc.RootElement.GetProperty("auth-session").GetString()!;

				var rawCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers.GetValueOrDefault("set-cookie")!);
				var cookies = string.Join("; ", [rawCookies, ProductCookie]);

				var requestBody = JsonSerializer.Serialize(new
				{
					stage_id = "AF-Stage-0e576350-a6e1-444e-a105-cb020f910845",
					stage_name = "Stage 1",
					formId = "AF-Form-a9a357e7-8b6d-416e-b974-04a2aa857e87",
					formValues = new
					{
						Your_address = new
						{
							postcode_search = new
							{
								value = postcode,
								path = "root/postcode_search",
							},
						},
					},
					formName = "WasteRecyclingCalendarForm",
					processId = "AF-Process-d615d6eb-6718-4e33-a2ff-18f1e5e58f8b",
				});

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 2,
					Url = $"https://my.northdevon.gov.uk/apibroker/runLookup?id=5849617f4ce25&app_name=AF-Renderer::Self&sid={sid}",
					Method = "POST",
					Headers = new Dictionary<string, string>
					{
						{ "content-type", "application/json" },
						{ "cookie", cookies },
					},
					Body = requestBody,
				};

				return new GetAddressesResponse
				{
					NextClientSideRequest = clientSideRequest
				};
			}

			// Step 3: Parse addresses
			else if (clientSideResponse.RequestId == 2)
			{
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
				var selectData = jsonDoc.RootElement.GetProperty("integration").GetProperty("transformed").GetProperty("select_data");

				var addresses = new List<Address>();
				foreach (var item in selectData.EnumerateArray())
				{
					var label = item.GetProperty("label").GetString()!.Trim();
					var value = item.GetProperty("value").GetString()!.Trim();

					addresses.Add(new Address
					{
						Property = label,
						Postcode = postcode,
						Uid = value,
					});
				}

				return new GetAddressesResponse
				{
					Addresses = addresses.AsReadOnly(),
				};
			}

			throw new InvalidOperationException("Invalid client-side request.");
		}

		/// <inheritdoc/>
		public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
		{
			// Step 1: Get auth session token (sid)
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = "https://my.northdevon.gov.uk/authapi/isauthenticated?uri=https%253A%252F%252Fmy.northdevon.gov.uk%252Fservice%252FWasteRecyclingCollectionCalendar&hostname=my.northdevon.gov.uk&withCredentials=true",
					Method = "GET",
					Headers = new Dictionary<string, string>
					{
						{ "cookie", ProductCookie },
					},
				};

				return new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest
				};
			}

			// Step 2: Retrieve live token
			else if (clientSideResponse.RequestId == 1)
			{
				// Include session and product cookies for subsequent lookups
				using var sidDoc = JsonDocument.Parse(clientSideResponse.Content);
				var sid = sidDoc.RootElement.GetProperty("auth-session").GetString()!;

				var rawCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers.GetValueOrDefault("set-cookie")!);
				var cookies = string.Join("; ", [rawCookies, ProductCookie]);

				var fullAddress = $"{address.Property} {address.Postcode}".Trim();

				var requestBody = JsonSerializer.Serialize(new
				{
					stage_id = "AF-Stage-0e576350-a6e1-444e-a105-cb020f910845",
					stage_name = "Stage 1",
					formId = "AF-Form-a9a357e7-8b6d-416e-b974-04a2aa857e87",
					formValues = new
					{
						Your_address = new
						{
							postcode_search = new
							{
								value = address.Postcode,
								path = "root/postcode_search",
							},
							chooseAddress = new
							{
								value = address.Uid,
								value_label = new List<string> { address.Property! },
								path = "root/chooseAddress",
							},
							uprnfromlookup = new
							{
								value = address.Uid,
								path = "root/uprnfromlookup",
							},
							UPRNMF = new
							{
								value = address.Uid,
								path = "root/UPRNMF",
							},
							FULLADDR2 = new
							{
								value = fullAddress,
								path = "root/FULLADDR2",
							},
						},
					},
					formName = "WasteRecyclingCalendarForm",
					processId = "AF-Process-d615d6eb-6718-4e33-a2ff-18f1e5e58f8b",
				});

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 2,
					Url = $"https://my.northdevon.gov.uk/apibroker/runLookup?id=59e606ee95b7a&app_name=AF-Renderer::Self&sid={sid}",
					Method = "POST",
					Headers = new Dictionary<string, string>
					{
						{ "content-type", "application/json" },
						{ "cookie", cookies },
					},
					Body = requestBody,
					Options = new ClientSideOptions
					{
						Metadata = new Dictionary<string, string>
						{
							{ "sid", sid },
							{ "cookies", cookies },
						},
					},
				};

				return new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest
				};
			}
			else if (clientSideResponse.RequestId == 2)
			{
				var metadata = clientSideResponse.Options.Metadata;
				var sid = metadata["sid"];

				// Merge returned cookies with carried-forward ones and product cookie
				var parsedCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers.GetValueOrDefault("set-cookie")!);
				var cookies = string.Join("; ", [metadata["cookies"], parsedCookies, ProductCookie]);

				using var tokenDoc = JsonDocument.Parse(clientSideResponse.Content);
				var liveToken = tokenDoc.RootElement
					.GetProperty("integration")
					.GetProperty("transformed")
					.GetProperty("rows_data")
					.GetProperty("0")
					.GetProperty("liveToken")
					.GetString()!;

				var requestBody = JsonSerializer.Serialize(new
				{
					stage_id = "AF-Stage-0e576350-a6e1-444e-a105-cb020f910845",
					stage_name = "Stage 1",
					formId = "AF-Form-a9a357e7-8b6d-416e-b974-04a2aa857e87",
					formValues = new
					{
						Calendar = new
						{
							uPRN = new
							{
								value = address.Uid,
								path = "root/uPRN",
							},
						},
					},
					formName = "WasteRecyclingCalendarForm",
					processId = "AF-Process-d615d6eb-6718-4e33-a2ff-18f1e5e58f8b",
				});

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 3,
					Url = $"https://my.northdevon.gov.uk/apibroker/runLookup?id=6255925ca44cb&app_name=AF-Renderer::Self&sid={sid}",
					Method = "POST",
					Headers = new Dictionary<string, string>
					{
						{ "content-type", "application/json" },
						{ "cookie", cookies },
					},
					Body = requestBody,
					Options = new ClientSideOptions
					{
						Metadata = new Dictionary<string, string>
						{
							{ "sid", sid },
							{ "cookies", cookies },
							{ "token", liveToken },
						},
					},
				};

				return new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest
				};
			}
			else if (clientSideResponse.RequestId == 3)
			{
				var metadata = clientSideResponse.Options.Metadata;
				var sid = metadata["sid"];
				var liveToken = metadata["token"];

				// Merge returned cookies with carried-forward ones and product cookie
				var parsedCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers.GetValueOrDefault("set-cookie")!);
				var cookies = string.Join("; ", [metadata["cookies"], parsedCookies, ProductCookie]);

				using var dateDoc = JsonDocument.Parse(clientSideResponse.Content);
				var dateRow = dateDoc.RootElement.GetProperty("integration").GetProperty("transformed").GetProperty("rows_data").GetProperty("0");

				var calendarStart = dateRow.GetProperty("calstartDate").GetString()!;
				var calendarEnd = dateRow.GetProperty("calendDate").GetString()!;

				var requestBody = JsonSerializer.Serialize(new
				{
					stage_id = "AF-Stage-0e576350-a6e1-444e-a105-cb020f910845",
					stage_name = "Stage 1",
					formId = "AF-Form-a9a357e7-8b6d-416e-b974-04a2aa857e87",
					formValues = new
					{
						Calendar = new
						{
							token = new
							{
								value = liveToken,
								path = "root/token",
							},
							uPRN = new
							{
								value = address.Uid,
								path = "root/uPRN",
							},
							calstartDate = new
							{
								value = calendarStart,
								path = "root/calstartDate",
							},
							calendDate = new
							{
								value = calendarEnd,
								path = "root/calendDate",
							},
						},
					},
					formName = "WasteRecyclingCalendarForm",
					processId = "AF-Process-d615d6eb-6718-4e33-a2ff-18f1e5e58f8b",
				});

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 4,
					Url = $"https://my.northdevon.gov.uk/apibroker/runLookup?id=61091d927cd81&app_name=AF-Renderer::Self&sid={sid}",
					Method = "POST",
					Headers = new Dictionary<string, string>
					{
						{ "content-type", "application/json" },
						{ "cookie", cookies },
					},
					Body = requestBody,
				};

				return new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest
				};
			}
			else if (clientSideResponse.RequestId == 4)
			{
				using var binsDoc = JsonDocument.Parse(clientSideResponse.Content);
				var rows = binsDoc.RootElement.GetProperty("integration").GetProperty("transformed").GetProperty("rows_data");

				// Build bin days from returned service details
				var binDays = new List<BinDay>();
				foreach (var row in rows.EnumerateObject())
				{
					var serviceDetail = row.Value.GetProperty("ServiceDetail").GetString()!.Trim();
					var workPack = row.Value.GetProperty("WorkPack").GetString()!.Trim();

					var dateMatch = ServiceDetailDateRegex().Match(serviceDetail);
					if (!dateMatch.Success)
					{
						continue;
					}

					var dateString = dateMatch.Groups["Date"].Value;
					var date = DateOnly.ParseExact(dateString, "dd/MM/yyyy", CultureInfo.InvariantCulture);

					var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, $"{workPack} {serviceDetail}");
					if (matchedBins.Count == 0)
					{
						continue;
					}

					binDays.Add(new BinDay
					{
						Date = date,
						Address = address,
						Bins = matchedBins,
					});
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
