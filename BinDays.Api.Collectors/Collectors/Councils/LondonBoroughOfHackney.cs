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

			return new GetAddressesResponse
			{
				NextClientSideRequest = new ClientSideRequest
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
				},
			};
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

				addresses.Add(new Address
				{
					Property = property,
					Postcode = postcode,
					Uid = systemId,
				});
			}

			return new GetAddressesResponse
			{
				Addresses = [.. addresses],
			};
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
			return CreateGetRequest(1, $"alloywastepages/getproperty/{address.Uid}");
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

			return CreateGetRequest(
				2,
				$"alloywastepages/getbin/{initialContainerIds[0]}",
				CreateStageMetadata(string.Join(",", initialContainerIds), 0, string.Empty, _stageBin)
			);
		}
		// Process container iteration stages (bin → collection → workflow)
		else if (clientSideResponse.RequestId >= 2)
		{
			return ProcessContainerStages(address, clientSideResponse);
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <summary>
	/// Processes the container iteration stages (bin → collection → workflow) for each container.
	/// </summary>
	private GetBinDaysResponse ProcessContainerStages(Address address, ClientSideResponse clientSideResponse)
	{
		var metadata = clientSideResponse.Options!.Metadata;
		var containerIdsMetadata = metadata["containerIds"];
		var containerIds = containerIdsMetadata.Split(
			",",
			StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
		);
		var containerIndex = int.Parse(metadata["containerIndex"], CultureInfo.InvariantCulture);
		var binDaysMetadata = metadata["binDays"];
		var stage = metadata["stage"];
		var nextRequestId = clientSideResponse.RequestId + 1;

		// Parse bin service name and request collection details
		if (stage == _stageBin)
		{
			using var binJson = JsonDocument.Parse(clientSideResponse.Content);
			var serviceName = binJson.RootElement.GetProperty("subTitle").GetString()!.Trim();

			var stageMetadata = CreateStageMetadata(containerIdsMetadata, containerIndex, binDaysMetadata, _stageCollection);
			stageMetadata["serviceName"] = serviceName;

			return CreateGetRequest(nextRequestId, $"alloywastepages/getcollection/{containerIds[containerIndex]}", stageMetadata);
		}
		// Parse collection schedule IDs and request first workflow
		else if (stage == _stageCollection)
		{
			using var collectionJson = JsonDocument.Parse(clientSideResponse.Content);
			var scheduleIdsElement = collectionJson.RootElement.GetProperty("scheduleCodeWorkflowIDs");

			// Iterate through each schedule id, and store it for workflow requests
			var scheduleIds = new List<string>();
			foreach (var scheduleId in scheduleIdsElement.EnumerateArray())
			{
				scheduleIds.Add(scheduleId.GetString()!);
			}

			var stageMetadata = CreateStageMetadata(containerIdsMetadata, containerIndex, binDaysMetadata, _stageWorkflow);
			stageMetadata["serviceName"] = metadata["serviceName"];
			stageMetadata["scheduleIds"] = string.Join(",", scheduleIds);
			stageMetadata["scheduleIndex"] = "0";

			return CreateGetRequest(nextRequestId, $"alloywastepages/getworkflow/{scheduleIds[0]}", stageMetadata);
		}
		// Process workflow dates and advance to next schedule, container, or finish
		else if (stage == _stageWorkflow)
		{
			var serviceName = metadata["serviceName"];
			var updatedBinDays = AccumulateBinDays(clientSideResponse.Content, serviceName, binDaysMetadata);

			var scheduleIds = metadata["scheduleIds"].Split(
				",",
				StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
			);
			var scheduleIndex = int.Parse(metadata["scheduleIndex"], CultureInfo.InvariantCulture) + 1;

			// More schedules to process for this container
			if (scheduleIndex < scheduleIds.Length)
			{
				var stageMetadata = CreateStageMetadata(containerIdsMetadata, containerIndex, updatedBinDays, _stageWorkflow);
				stageMetadata["serviceName"] = serviceName;
				stageMetadata["scheduleIds"] = metadata["scheduleIds"];
				stageMetadata["scheduleIndex"] = scheduleIndex.ToString(CultureInfo.InvariantCulture);

				return CreateGetRequest(nextRequestId, $"alloywastepages/getworkflow/{scheduleIds[scheduleIndex]}", stageMetadata);
			}

			containerIndex++;

			// More containers to process
			if (containerIndex < containerIds.Length)
			{
				return CreateGetRequest(
					nextRequestId,
					$"alloywastepages/getbin/{containerIds[containerIndex]}",
					CreateStageMetadata(containerIdsMetadata, containerIndex, updatedBinDays, _stageBin)
				);
			}

			// All containers processed, parse and return final bin days
			return new GetBinDaysResponse
			{
				BinDays = ParseBinDays(updatedBinDays, address),
			};
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <summary>
	/// Creates the base metadata dictionary for container iteration stages.
	/// </summary>
	private static Dictionary<string, string> CreateStageMetadata(string containerIds, int containerIndex, string binDays, string stage)
	{
		return new()
		{
			{ "containerIds", containerIds },
			{ "containerIndex", containerIndex.ToString(CultureInfo.InvariantCulture) },
			{ "binDays", binDays },
			{ "stage", stage },
		};
	}

	/// <summary>
	/// Creates a GET request to the Hackney waste API.
	/// </summary>
	private static GetBinDaysResponse CreateGetRequest(int requestId, string endpoint, Dictionary<string, string>? metadata = null)
	{
		return new GetBinDaysResponse
		{
			NextClientSideRequest = new ClientSideRequest
			{
				RequestId = requestId,
				Url = $"{_baseApiUrl}/{endpoint}",
				Method = "GET",
				Options = metadata != null
					? new ClientSideOptions { Metadata = metadata }
					: new(),
			},
		};
	}

	/// <summary>
	/// Extracts dates from a workflow response and appends them to the accumulated bin days metadata.
	/// </summary>
	private static string AccumulateBinDays(string workflowContent, string serviceName, string existingBinDays)
	{
		using var workflowJson = JsonDocument.Parse(workflowContent);
		var datesElement = workflowJson.RootElement
			.GetProperty("trigger")
			.GetProperty("dates");

		// Iterate through each workflow date, and store a bin day entry
		var binDayEntries = new List<string>();
		foreach (var dateElement in datesElement.EnumerateArray())
		{
			binDayEntries.Add($"{dateElement.GetString()!}|{serviceName}");
		}

		var newEntries = string.Join(';', binDayEntries);

		return existingBinDays.Length > 0 && newEntries.Length > 0
			? $"{existingBinDays};{newEntries}"
			: $"{existingBinDays}{newEntries}";
	}

	/// <summary>
	/// Parses accumulated bin day metadata into BinDay objects.
	/// </summary>
	private IReadOnlyCollection<BinDay> ParseBinDays(string binDaysMetadata, Address address)
	{
		// Iterate through each bin day entry, and create a new bin day object
		var binDays = new List<BinDay>();
		var entries = binDaysMetadata.Split(
			";",
			StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
		);

		foreach (var entry in entries)
		{
			var parts = entry.Split("|", 2);
			var dateTime = DateTime.Parse(
				parts[0],
				CultureInfo.InvariantCulture,
				DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal
			);

			binDays.Add(new BinDay
			{
				Date = DateOnly.FromDateTime(dateTime),
				Address = address,
				Bins = ProcessingUtilities.GetMatchingBins(_binTypes, parts[1]),
			});
		}

		return ProcessingUtilities.ProcessBinDays(binDays);
	}
}
