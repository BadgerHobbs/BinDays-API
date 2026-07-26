namespace BinDays.Api.Collectors.Collectors.Vendors;

using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Base collector implementation for councils using the South and Vale BinDays API.
/// </summary>
internal abstract class BinzoneCollectorBase : GovUkCollectorBase
{
	/// <summary>
	/// The council code used by the BinDays API ("S" for South Oxfordshire or "V" for Vale).
	/// </summary>
	protected abstract string CouncilCode { get; }

	/// <summary>
	/// The list of bin types for this collector.
	/// </summary>
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "Rubbish",
			Colour = BinColour.Black,
			Keys = [ "Non-recyclable refuse waste" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Green,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Green,
			Keys = [ "Food waste" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden Waste subscribers" ],
		},
		new()
		{
			Name = "Small Electrical Items",
			Colour = BinColour.Any,
			Keys = [ "Small electricals" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Textiles",
			Colour = BinColour.Any,
			Keys = [ "Textiles/Clothes" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Batteries",
			Colour = BinColour.Clear,
			Keys = [ "Batteries" ],
			Type = BinType.Bag,
		},
	];

	/// <summary>
	/// The base URL for the BinDays property API.
	/// </summary>
	private const string _propertyApiBaseUrl = "https://forms.southandvale.gov.uk/api/property";

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_propertyApiBaseUrl}/postcode/{postcode}",
				Method = "GET",
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
			using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);
			var setData = jsonDocument.RootElement.GetProperty("setData");

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (var addressItem in setData.EnumerateArray())
			{
				var council = addressItem.GetProperty("council").GetString()!;

				if (council != CouncilCode)
				{
					continue;
				}

				var address = new Address
				{
					Property = addressItem.GetProperty("address").GetString()!,
					Postcode = postcode,
					Uid = addressItem.GetProperty("uprn").GetString()!,
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
		// TODO: Remove once legacy UIDs are no longer in circulation. Addresses are cached both
		// server-side (30-day TTL) and client-side (indefinitely), so this may need to stay long-term.
		// Clients that cached addresses before the migration to the South and Vale JSON API still
		// hold UIDs in the legacy ebase Binzone form format (CTRL:63:_:D:N). These cannot be used
		// directly as UPRNs, so they are resolved via a postcode lookup before fetching bin days.
		if (address.Uid!.Contains(':'))
		{
			return GetLegacyBinDays(address, clientSideResponse);
		}

		// Prepare client-side request for getting bin days
		if (clientSideResponse == null)
		{
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"{_propertyApiBaseUrl}/bins/{address.Uid}",
				Method = "GET",
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 1)
		{
			using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);
			var setData = jsonDocument.RootElement.GetProperty("setData");
			var binDays = new List<BinDay>();

			if (setData.GetProperty("site").GetString()! != CouncilCode)
			{
				throw new InvalidOperationException("Address does not belong to this council.");
			}

			// Iterate through each collection week
			foreach (var week in setData.GetProperty("week").EnumerateArray())
			{
				// Iterate through each collection day, and create bin day objects
				foreach (var day in week.GetProperty("day").EnumerateArray())
				{
					var collectionDate = day.GetProperty("collection_date").GetString()!;
					var date = DateUtilities.ParseDateExact(collectionDate, "dd/MM/yyyy");

					// Iterate through each bin entry for the collection day
					foreach (var bin in day.GetProperty("bins").EnumerateArray())
					{
						var service = bin.GetProperty("bin_type").GetString()!;
						var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

						if (matchedBins.Count == 0)
						{
							continue;
						}

						var binDay = new BinDay
						{
							Date = date,
							Address = address,
							Bins = matchedBins,
						};

						binDays.Add(binDay);
					}
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
	/// Handles bin days requests for legacy UIDs (CTRL:63:_:D:N format).
	/// Resolves the UPRN by fetching addresses for the postcode and selecting by the index encoded in the legacy UID.
	/// </summary>
	private GetBinDaysResponse GetLegacyBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		if (clientSideResponse == null)
		{
			return new GetBinDaysResponse
			{
				NextClientSideRequest = new ClientSideRequest
				{
					RequestId = 1,
					Url = $"{_propertyApiBaseUrl}/postcode/{address.Postcode}",
					Method = "GET",
				},
			};
		}

		if (clientSideResponse.RequestId == 1)
		{
			var uid = address.Uid!;
			var index = int.Parse(uid.AsSpan(uid.LastIndexOf(':') + 1));

			using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);
			var setData = jsonDocument.RootElement.GetProperty("setData");

			var uprn = setData.EnumerateArray()
				.Where(item => item.GetProperty("council").GetString() == CouncilCode)
				.ElementAt(index)
				.GetProperty("uprn").GetString()!;

			return new GetBinDaysResponse
			{
				NextClientSideRequest = new ClientSideRequest
				{
					RequestId = 2,
					Url = $"{_propertyApiBaseUrl}/bins/{uprn}",
					Method = "GET",
				},
			};
		}

		if (clientSideResponse.RequestId == 2)
		{
			using var jsonDocument = JsonDocument.Parse(clientSideResponse.Content);
			var setData = jsonDocument.RootElement.GetProperty("setData");
			var binDays = new List<BinDay>();

			if (setData.GetProperty("site").GetString()! != CouncilCode)
			{
				throw new InvalidOperationException("Address does not belong to this council.");
			}

			// Iterate through each collection week
			foreach (var week in setData.GetProperty("week").EnumerateArray())
			{
				// Iterate through each collection day, and create bin day objects
				foreach (var day in week.GetProperty("day").EnumerateArray())
				{
					var collectionDate = day.GetProperty("collection_date").GetString()!;
					var date = DateUtilities.ParseDateExact(collectionDate, "dd/MM/yyyy");

					// Iterate through each bin entry for the collection day
					foreach (var bin in day.GetProperty("bins").EnumerateArray())
					{
						var service = bin.GetProperty("bin_type").GetString()!;
						var matchedBins = ProcessingUtilities.GetMatchingBins(_binTypes, service);

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
				}
			}

			return new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}
}
