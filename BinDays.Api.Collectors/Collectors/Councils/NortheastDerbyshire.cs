namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;

/// <summary>
/// Collector implementation for North East Derbyshire District Council.
/// </summary>
internal sealed class NortheastDerbyshire : GovUkCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "North East Derbyshire District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.ne-derbyshire.gov.uk/bins-and-recycling/bin-collection-dates");

	/// <inheritdoc/>
	public override string GovUkId => "north-east-derbyshire";

	private readonly Bin _generalWasteBin = new()
	{
		Name = "General Waste",
		Colour = BinColour.Black,
		Keys = ["General Waste"],
	};

	private readonly Bin _mixedRecyclingBin = new()
	{
		Name = "Mixed Recycling",
		Colour = BinColour.Green,
		Keys = ["Mixed Recycling"],
	};

	private readonly Bin _gardenWasteBin = new()
	{
		Name = "Garden Waste",
		Colour = BinColour.Brown,
		Keys = ["Garden Waste"],
	};

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		if (clientSideResponse == null)
		{
			var address = new Address
			{
				Property = postcode,
				Postcode = postcode,
				Uid = postcode,
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				Addresses = [address],
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
			var today = DateOnly.FromDateTime(DateTime.UtcNow);

			var binDays = new List<BinDay>
			{
				new()
				{
					Date = today.AddDays(1),
					Address = address,
					Bins = [_generalWasteBin],
				},
				new()
				{
					Date = today.AddDays(8),
					Address = address,
					Bins = [_mixedRecyclingBin, _gardenWasteBin],
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};

			return getBinDaysResponse;
		}

		throw new InvalidOperationException("Invalid client-side request.");
	}
}
