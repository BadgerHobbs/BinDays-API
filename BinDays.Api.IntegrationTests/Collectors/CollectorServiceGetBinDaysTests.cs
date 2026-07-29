namespace BinDays.Api.IntegrationTests.Collectors;

using BinDays.Api.Collectors.Collectors;
using BinDays.Api.Collectors.Exceptions;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

/// <summary>
/// Tests that <see cref="CollectorService.GetBinDays"/> separates bin days that matched no bin
/// type from those that did, rather than treating a partial or total mismatch the same as a
/// genuine absence of scheduled collections.
/// </summary>
public sealed class CollectorServiceGetBinDaysTests
{
	private static readonly Bin _generalWaste = new()
	{
		Name = "General Waste",
		Colour = BinColour.Black,
		Keys = ["General"],
	};

	[Fact]
	public void GetBinDays_WithSomeBinDaysUnmatched_DropsThemAndReturnsTheRest()
	{
		var address = new Address { Postcode = "AB1 2CD", Uid = "1" };
		var matchedBinDay = new BinDay { Date = new DateOnly(2026, 1, 1), Address = address, Bins = [_generalWaste] };
		var unmatchedBinDay = new BinDay { Date = new DateOnly(2026, 1, 8), Address = address, Bins = [] };

		var collector = new FakeCollector([unmatchedBinDay, matchedBinDay]);
		var collectorService = new CollectorService([collector], NullLogger<CollectorService>.Instance);

		var result = collectorService.GetBinDays(FakeCollector.GovUkId, address, null);

		var resultBinDay = Assert.Single(result.BinDays!);
		Assert.Same(matchedBinDay, resultBinDay);
	}

	[Fact]
	public void GetBinDays_WithAllBinDaysUnmatched_ThrowsAllBinDaysUnmatchedException()
	{
		var address = new Address { Postcode = "AB1 2CD", Uid = "1" };
		var unmatchedBinDay = new BinDay { Date = new DateOnly(2026, 1, 1), Address = address, Bins = [] };

		var collector = new FakeCollector([unmatchedBinDay]);
		var collectorService = new CollectorService([collector], NullLogger<CollectorService>.Instance);

		Assert.Throws<AllBinDaysUnmatchedException>(() => collectorService.GetBinDays(FakeCollector.GovUkId, address, null));
	}

	[Fact]
	public void GetBinDays_WithNoBinDays_ThrowsBinDaysNotFoundException()
	{
		var address = new Address { Postcode = "AB1 2CD", Uid = "1" };

		var collector = new FakeCollector([]);
		var collectorService = new CollectorService([collector], NullLogger<CollectorService>.Instance);

		Assert.Throws<BinDaysNotFoundException>(() => collectorService.GetBinDays(FakeCollector.GovUkId, address, null));
	}

	/// <summary>
	/// A minimal <see cref="ICollector"/> stub that returns a fixed set of bin days with no
	/// client-side requests required.
	/// </summary>
	private sealed class FakeCollector(IReadOnlyCollection<BinDay> binDays) : ICollector
	{
		public static string Name => "Fake Collector";

		public static Uri WebsiteUrl => new("https://example.com");

		public static string GovUkId => "fake-collector";

		public static Uri GovUkUrl => new("https://www.gov.uk/find-out-when-your-bin-collection-day-is/fake-collector");

		public static int Version => 1;

		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse) =>
			throw new NotImplementedException();

		public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse) =>
			new() { BinDays = binDays };
	}
}
