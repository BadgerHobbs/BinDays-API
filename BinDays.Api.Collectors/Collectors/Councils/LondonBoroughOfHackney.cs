namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

/// <summary>
/// Collector implementation for London Borough of Hackney.
/// </summary>
internal sealed class LondonBoroughOfHackney : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "London Borough of Hackney";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://hackney.gov.uk/rubbish");

	/// <inheritdoc/>
	public override string GovUkId => "hackney";

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "Dry Recycling (Sacks)",
			Colour = BinColour.Green,
			Type = BinType.Bag,
			Keys = [ "Recycling Sack" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Grey,
			Type = BinType.Caddy,
			Keys = [ "Food Caddy (Small)" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "GW_Wheeled Bin 140l" ],
		},
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Black,
			Keys = [ "Wheeled Bin (180ltr)" ],
		},
	];

	/// <summary>
	/// The base URL for the Hackney waste API.
	/// </summary>
	private const string _baseApiUrl = "https://waste-api-hackney-live.ieg4.net/f806d91c-e133-43a6-ba9a-c0ae4f4cccf6";

	/// <summary>
	/// The metadata stage value for bin requests.
	/// </summary>
	private const string _stageBin = "bin";

	/// <summary>
	/// The metadata stage value for collection requests.
	/// </summary>
	private const string _stageCollection = "collection";

	/// <summary>
	/// The metadata stage value for workflow requests.
	/// </summary>
	private const string _stageWorkflow = "workflow";

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var requestBody = $$"""
			{
				"Postcode":"{{postcode}}",
				"Filters":[{"Filter":"attributes_premisesBlpuClass","Include":true,"StringMatch":"Prefix","Value":"R"}]
			}
			""";

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseApiUrl}/property/opensearch",
				Method = "POST",
				Headers = new()
				{
					{ "content-type", Constants.ApplicationJson },
					{ "user-agent", Constants.UserAgent },
				},
				Body = requestBody,
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
			using var responseJson = JsonDocument.Parse(clientSideResponse.Content);
			var addressesElement = responseJson.RootElement.GetProperty("addressSummaries");

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var addressElement in addressesElement.EnumerateArray())
			{
				var summary = addressElement.GetProperty("summary").GetString()!;
				var systemId = addressElement.GetProperty("systemId").GetString()!;

				var property = string.Join(
					" ",
					summary.Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				);

				var address = new Address
				{
					Property = property,
					Postcode = postcode,
					Uid = systemId,
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
		// Prepare client-side request for getting property details
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_baseApiUrl}/alloywastepages/getproperty/{address.Uid}",
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
		// Parse property response and request first bin details
		else if (clientSideResponse.RequestId == 1)
		{
			using var propertyJson = JsonDocument.Parse(clientSideResponse.Content);
			var containersRaw = propertyJson.RootElement
				.GetProperty("providerSpecificFields")
				.GetProperty("attributes_wasteContainersAssignableWasteContainers")
				.GetString()!;

			var initialContainerIds = containersRaw.Split(
				",",
				StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
			);

			var initialContainerIdsMetadata = string.Join(",", initialContainerIds);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 2,
				Url = $"{_baseApiUrl}/alloywastepages/getbin/{initialContainerIds[0]}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "containerIds", initialContainerIdsMetadata },
						{ "containerIndex", "0" },
						{ "binDays", string.Empty },
						{ "stage", _stageBin },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}

		var metadata = clientSideResponse.Options!.Metadata;
		var containerIdsMetadata = metadata["containerIds"];
		var containerIds = containerIdsMetadata.Split(
			",",
			StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
		);
		var containerIndex = int.Parse(metadata["containerIndex"], CultureInfo.InvariantCulture);
		var binDaysMetadata = metadata["binDays"];
		var stage = metadata["stage"];

		if (stage == _stageBin)
		{
			using var binJson = JsonDocument.Parse(clientSideResponse.Content);
			var serviceName = binJson.RootElement.GetProperty("subTitle").GetString()!.Trim();

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = clientSideResponse.RequestId + 1,
				Url = $"{_baseApiUrl}/alloywastepages/getcollection/{containerIds[containerIndex]}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "containerIds", containerIdsMetadata },
						{ "containerIndex", containerIndex.ToString(CultureInfo.InvariantCulture) },
						{ "binDays", binDaysMetadata },
						{ "stage", _stageCollection },
						{ "serviceName", serviceName },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		else if (stage == _stageCollection)
		{
			using var collectionJson = JsonDocument.Parse(clientSideResponse.Content);
			var scheduleIdsElement = collectionJson.RootElement.GetProperty("scheduleCodeWorkflowIDs");

			var scheduleIds = new List<string>();
			// Iterate through each schedule id, and store it for workflow requests
			foreach (var scheduleId in scheduleIdsElement.EnumerateArray())
			{
				scheduleIds.Add(scheduleId.GetString()!);
			}

			var scheduleIdsMetadata = string.Join(",", scheduleIds);

			var clientSideRequest = new ClientSideRequest
			{
				RequestId = clientSideResponse.RequestId + 1,
				Url = $"{_baseApiUrl}/alloywastepages/getworkflow/{scheduleIds[0]}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
				Options = new ClientSideOptions
				{
					Metadata =
					{
						{ "containerIds", containerIdsMetadata },
						{ "containerIndex", containerIndex.ToString(CultureInfo.InvariantCulture) },
						{ "binDays", binDaysMetadata },
						{ "stage", _stageWorkflow },
						{ "serviceName", metadata["serviceName"] },
						{ "scheduleIds", scheduleIdsMetadata },
						{ "scheduleIndex", "0" },
					},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		else if (stage == _stageWorkflow)
		{
			using var workflowJson = JsonDocument.Parse(clientSideResponse.Content);
			var datesElement = workflowJson.RootElement
				.GetProperty("trigger")
				.GetProperty("dates");

			var binDayEntries = new List<string>();
			var serviceName = metadata["serviceName"];
			// Iterate through each workflow date, and store a bin day entry
			foreach (var dateElement in datesElement.EnumerateArray())
			{
				binDayEntries.Add($"{dateElement.GetString()!}|{serviceName}");
			}

			var newEntries = string.Join(';', binDayEntries);
			var binDaysParts = new List<string>();
			if (binDaysMetadata.Length > 0)
			{
				binDaysParts.Add(binDaysMetadata);
			}
			if (newEntries.Length > 0)
			{
				binDaysParts.Add(newEntries);
			}
			var binDaysBuilder = string.Join(';', binDaysParts);

			var scheduleIds = metadata["scheduleIds"].Split(
				",",
				StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
			);
			var scheduleIndex = int.Parse(metadata["scheduleIndex"], CultureInfo.InvariantCulture) + 1;

			if (scheduleIndex < scheduleIds.Length)
			{
				var clientSideRequest = new ClientSideRequest
				{
					RequestId = clientSideResponse.RequestId + 1,
					Url = $"{_baseApiUrl}/alloywastepages/getworkflow/{scheduleIds[scheduleIndex]}",
					Method = "GET",
					Headers = new()
					{
						{ "user-agent", Constants.UserAgent },
					},
					Options = new ClientSideOptions
					{
						Metadata =
						{
							{ "containerIds", containerIdsMetadata },
							{ "containerIndex", containerIndex.ToString(CultureInfo.InvariantCulture) },
							{ "binDays", binDaysBuilder },
							{ "stage", _stageWorkflow },
							{ "serviceName", serviceName },
							{ "scheduleIds", metadata["scheduleIds"] },
							{ "scheduleIndex", scheduleIndex.ToString(CultureInfo.InvariantCulture) },
						},
					},
				};

				var workflowStepResponse = new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest,
				};

				return workflowStepResponse;
			}

			containerIndex++;

			if (containerIndex < containerIds.Length)
			{
				var clientSideRequest = new ClientSideRequest
				{
					RequestId = clientSideResponse.RequestId + 1,
					Url = $"{_baseApiUrl}/alloywastepages/getbin/{containerIds[containerIndex]}",
					Method = "GET",
					Headers = new()
					{
						{ "user-agent", Constants.UserAgent },
					},
					Options = new ClientSideOptions
					{
						Metadata =
						{
							{ "containerIds", containerIdsMetadata },
							{ "containerIndex", containerIndex.ToString(CultureInfo.InvariantCulture) },
							{ "binDays", binDaysBuilder },
							{ "stage", _stageBin },
						},
					},
				};

				var nextContainerResponse = new GetBinDaysResponse
				{
					NextClientSideRequest = clientSideRequest,
				};

				return nextContainerResponse;
			}

			var binDays = new List<BinDay>();
			var binDayEntriesMetadata = binDaysBuilder.Split(
				";",
				StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
			);

			// Iterate through each bin day entry, and create a new bin day object
			foreach (var binDayEntry in binDayEntriesMetadata)
			{
				var parts = binDayEntry.Split("|", 2);
				var dateTime = DateTime.Parse(
					parts[0],
					CultureInfo.InvariantCulture,
					DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal
				);

				var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, parts[1]);

				var binDay = new BinDay
				{
					Date = DateOnly.FromDateTime(dateTime),
					Address = address,
					Bins = matchedBins,
				};

				binDays.Add(binDay);
			}

			var completedBinDaysResponse = new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};

			return completedBinDaysResponse;
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}
}
