namespace BinDays.Api.IntegrationTests.Collectors;

using BinDays.Api.Collectors.Collectors;
using BinDays.Api.Collectors.Exceptions;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Services;
using BinDays.Api.Collectors.Telemetry;
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
		var collectorService = new CollectorService([collector], NullLogger<CollectorService>.Instance, NullCollectorMetrics.Instance);

		var result = collectorService.GetBinDays(collector.GovUkId, address, null);

		var resultBinDay = Assert.Single(result.BinDays!);
		Assert.Same(matchedBinDay, resultBinDay);
	}

	[Fact]
	public void GetBinDays_WithAllBinDaysUnmatched_ThrowsAllBinDaysUnmatchedException()
	{
		var address = new Address { Postcode = "AB1 2CD", Uid = "1" };
		var unmatchedBinDay = new BinDay { Date = new DateOnly(2026, 1, 1), Address = address, Bins = [] };

		var collector = new FakeCollector([unmatchedBinDay]);
		var collectorService = new CollectorService([collector], NullLogger<CollectorService>.Instance, NullCollectorMetrics.Instance);

		Assert.Throws<AllBinDaysUnmatchedException>(() => collectorService.GetBinDays(collector.GovUkId, address, null));
	}

	[Fact]
	public void GetBinDays_WithNoBinDays_ThrowsBinDaysNotFoundException()
	{
		var address = new Address { Postcode = "AB1 2CD", Uid = "1" };

		var collector = new FakeCollector([]);
		var collectorService = new CollectorService([collector], NullLogger<CollectorService>.Instance, NullCollectorMetrics.Instance);

		Assert.Throws<BinDaysNotFoundException>(() => collectorService.GetBinDays(collector.GovUkId, address, null));
	}

	[Fact]
	public void GetBinDays_WithSomeBinDaysUnmatched_RecordsOneMetricPerDroppedBinDay()
	{
		var address = new Address { Postcode = "AB1 2CD", Uid = "1" };
		var matchedBinDay = new BinDay { Date = new DateOnly(2026, 1, 1), Address = address, Bins = [_generalWaste] };
		var firstUnmatched = new BinDay { Date = new DateOnly(2026, 1, 8), Address = address, Bins = [] };
		var secondUnmatched = new BinDay { Date = new DateOnly(2026, 1, 15), Address = address, Bins = [] };

		var collector = new FakeCollector([firstUnmatched, matchedBinDay, secondUnmatched]);
		var metrics = new RecordingCollectorMetrics();
		var collectorService = new CollectorService([collector], NullLogger<CollectorService>.Instance, metrics);

		collectorService.GetBinDays(collector.GovUkId, address, null);

		Assert.Equal([collector.GovUkId, collector.GovUkId], metrics.UnmatchedGovUkIds);
	}

	[Fact]
	public void GetBinDays_WithAllBinDaysMatched_RecordsNoMetric()
	{
		var address = new Address { Postcode = "AB1 2CD", Uid = "1" };
		var matchedBinDay = new BinDay { Date = new DateOnly(2026, 1, 1), Address = address, Bins = [_generalWaste] };

		var collector = new FakeCollector([matchedBinDay]);
		var metrics = new RecordingCollectorMetrics();
		var collectorService = new CollectorService([collector], NullLogger<CollectorService>.Instance, metrics);

		collectorService.GetBinDays(collector.GovUkId, address, null);

		Assert.Empty(metrics.UnmatchedGovUkIds);
	}

	/// <summary>
	/// An <see cref="ICollectorMetrics"/> that captures what it was asked to record.
	/// </summary>
	private sealed class RecordingCollectorMetrics : ICollectorMetrics
	{
		/// <summary>
		/// The gov.uk identifier of every dropped bin day, in the order recorded.
		/// </summary>
		public List<string> UnmatchedGovUkIds { get; } = [];

		/// <inheritdoc/>
		public void RecordBinDayUnmatched(string govUkId)
		{
			UnmatchedGovUkIds.Add(govUkId);
		}
	}

	/// <summary>
	/// A minimal <see cref="ICollector"/> stub that returns a fixed set of bin days with no
	/// client-side requests required.
	/// </summary>
	private sealed class FakeCollector : ICollector
	{
		private readonly IReadOnlyCollection<BinDay> _binDays;

		public FakeCollector(IReadOnlyCollection<BinDay> binDays)
		{
			_binDays = binDays;
		}

		public string Name { get; } = "Fake Collector";

		public Uri WebsiteUrl { get; } = new("https://example.com");

		public string GovUkId { get; } = "fake-collector";

		public Uri GovUkUrl { get; } = new("https://www.gov.uk/find-out-when-your-bin-collection-day-is/fake-collector");

		public int Version { get; } = 1;

		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse) =>
			throw new NotImplementedException();

		public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse) =>
			new() { BinDays = _binDays };
	}
}
