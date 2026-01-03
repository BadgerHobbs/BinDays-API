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
	/// Collector implementation for Tunbridge Wells Borough Council.
	/// </summary>
	internal sealed partial class TunbridgeWellsBoroughCouncil : GovUkCollectorBase, ICollector
	{
		private const string AchieveFormsUrl = "https://tunbridgewells-self.achieveservice.com/AchieveForms/?mode=fill&consentMessage=yes&form_uri=sandbox-publish://AF-Process-e01af4d4-eb0f-4cfe-a5ac-c47b63f017ed/AF-Stage-88caf66c-378f-4082-ad1d-07b7a850af38/definition.json&process=1&process_uri=sandbox-processes://AF-Process-e01af4d4-eb0f-4cfe-a5ac-c47b63f017ed&process_id=AF-Process-e01af4d4-eb0f-4cfe-a5ac-c47b63f017ed";
		private const string StageId = "AF-Stage-88caf66c-378f-4082-ad1d-07b7a850af38";
		private const string FormId = "AF-Form-df7cac1a-c096-4c38-9488-85619a7646bb";
		private const string ProcessId = "AF-Process-e01af4d4-eb0f-4cfe-a5ac-c47b63f017ed";
		private const string FormName = "Bin day collection checker 2022";
		private const string FormUri = "sandbox-publish://AF-Process-e01af4d4-eb0f-4cfe-a5ac-c47b63f017ed/AF-Stage-88caf66c-378f-4082-ad1d-07b7a850af38/definition.json";
		private const string AddressLookupId = "11c66af5fbe94";
		private const string CollectionsLookupId = "6314720683f30";

		/// <inheritdoc/>
		public string Name => "Tunbridge Wells Borough Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://tunbridgewells-self.achieveservice.com/service/Check_your_bin_collection_day");

		/// <inheritdoc/>
		public override string GovUkId => "tunbridge-wells";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly IReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "General Waste",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Refuse" }.AsReadOnly(),
			},
			new()
			{
				Name = "Recycling Bin",
				Colour = BinColour.Brown,
				Keys = new List<string>() { "Recycling" }.AsReadOnly(),
			},
			new()
			{
				Name = "Paper Recycling",
				Colour = BinColour.Green,
				Keys = new List<string>() { "Recycling" }.AsReadOnly(),
				Type = BinType.Box,
			},
			new()
			{
				Name = "Food Waste",
				Colour = BinColour.Yellow,
				Keys = new List<string>() { "Food", "Recycling", "Refuse" }.AsReadOnly(),
				Type = BinType.Caddy,
			},
			new()
			{
				Name = "Garden Waste",
				Colour = BinColour.Black,
				Keys = new List<string>() { "Garden" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <summary>
		/// Regex to extract the session identifier (sid) from HTML.
		/// </summary>
		[GeneratedRegex(@"sid=([a-f0-9]+)")]
		private static partial Regex SessionIdRegex();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = AchieveFormsUrl,
					Method = "GET",
					Headers = new Dictionary<string, string>
					{
						{ "User-Agent", Constants.UserAgent },
					},
				};

				return new GetAddressesResponse
				{
					NextClientSideRequest = clientSideRequest,
				};
			}
			else if (clientSideResponse.RequestId == 1)
			{
				var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);
				var sessionId = SessionIdRegex().Match(clientSideResponse.Content).Groups[1].Value;

				var requestBody = JsonSerializer.Serialize(new
				{
					stopOnFailure = true,
					usePHPIntegrations = true,
					stage_id = StageId,
					stage_name = "Customer form",
					formId = FormId,
					formValues = new
					{
						Property = new
						{
							gardenSuspended = new { value = "no" },
							postcode_search = new { value = ProcessingUtilities.FormatPostcode(postcode) },
							postcodeValid = new { value = "true" },
							buttonPressed = new { value = string.Empty },
							selectedAddress = new { value = string.Empty },
							propertyReference = new { value = string.Empty },
							siteReference = new { value = string.Empty },
						},
					},
					isPublished = true,
					formName = FormName,
					processId = ProcessId,
					formUri = FormUri,
				});

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 2,
					Url = $"https://tunbridgewells-self.achieveservice.com/apibroker/runLookup?id={AddressLookupId}&repeat_against=&noRetry=false&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&sid={sessionId}",
					Method = "POST",
					Headers = new Dictionary<string, string>
					{
						{ "Content-Type", "application/json" },
						{ "cookie", requestCookies },
					},
					Body = requestBody,
				};

				return new GetAddressesResponse
				{
					NextClientSideRequest = clientSideRequest,
				};
			}
			else if (clientSideResponse.RequestId == 2)
			{
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
				var rows = jsonDoc.RootElement
					.GetProperty("integration")
					.GetProperty("transformed")
					.GetProperty("rows_data");

				var addresses = new List<Address>();
				foreach (var row in rows.EnumerateObject())
				{
					var addressData = row.Value;

					var address = new Address
					{
						Property = addressData.GetProperty("display").GetString()!.Trim(),
						Postcode = ProcessingUtilities.FormatPostcode(postcode),
						Uid = addressData.GetProperty("uprn").GetString()!.Trim(),
					};

					addresses.Add(address);
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
			if (clientSideResponse == null)
			{
				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = AchieveFormsUrl,
					Method = "GET",
					Headers = new Dictionary<string, string>
					{
						{ "User-Agent", Constants.UserAgent },
					},
				};

				return new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest,
				};
			}
			else if (clientSideResponse.RequestId == 1)
			{
				var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(clientSideResponse.Headers["set-cookie"]);
				var sessionId = SessionIdRegex().Match(clientSideResponse.Content).Groups[1].Value;

				var requestBody = JsonSerializer.Serialize(new
				{
					stopOnFailure = true,
					usePHPIntegrations = true,
					stage_id = StageId,
					stage_name = "Customer form",
					formId = FormId,
					formValues = new
					{
						Property = new
						{
							gardenSuspended = new { value = "no" },
							postcodeValid = new { value = "true" },
							buttonPressed = new { value = "Yes" },
							addressPicker = new { value = address.Uid },
							selectedAddress = new { value = address.Property },
							waitCounter = new { value = "0" },
							propertyReference = new { value = address.Uid },
							siteReference = new { value = address.Uid },
							siteID = new { value = "0" },
							totalCollections = new { value = "0" },
							postcodeEntered = new { value = ProcessingUtilities.FormatPostcode(address.Postcode) },
							accountSiteType = new { value = string.Empty },
							classificationCode = new { value = string.Empty },
							classificationDescription = new { value = string.Empty },
							custodian = new { value = string.Empty },
							parishName = new { value = string.Empty },
							wardName = new { value = string.Empty },
							referredBy = new { value = "binchecker" },
							classType = new { value = string.Empty },
						},
					},
					isPublished = true,
					formName = FormName,
					processId = ProcessId,
					formUri = FormUri,
				});

				var clientSideRequest = new ClientSideRequest
				{
					RequestId = 2,
					Url = $"https://tunbridgewells-self.achieveservice.com/apibroker/runLookup?id={CollectionsLookupId}&repeat_against=&noRetry=false&getOnlyTokens=undefined&log_id=&app_name=AF-Renderer::Self&sid={sessionId}",
					Method = "POST",
					Headers = new Dictionary<string, string>
					{
						{ "Content-Type", "application/json" },
						{ "cookie", requestCookies },
					},
					Body = requestBody,
				};

				return new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest,
				};
			}
			else if (clientSideResponse.RequestId == 2)
			{
				using var jsonDoc = JsonDocument.Parse(clientSideResponse.Content);
				var rows = jsonDoc.RootElement
					.GetProperty("integration")
					.GetProperty("transformed")
					.GetProperty("rows_data");

				var binDays = new List<BinDay>();
				foreach (var row in rows.EnumerateObject())
				{
					var collection = row.Value;
					var collectionType = collection.GetProperty("collectionType").GetString()!.Trim();
					var collectionDate = collection.GetProperty("nextDateUnformatted").GetString()!.Trim();

					var date = DateOnly.ParseExact(collectionDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
					var bins = ProcessingUtilities.GetMatchingBins(_binTypes, collectionType);

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
