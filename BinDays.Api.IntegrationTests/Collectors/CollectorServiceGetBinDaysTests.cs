namespace BinDays.Api.IntegrationTests.Collectors;

using BinDays.Api.Collectors.Collectors;
using BinDays.Api.Collectors.Exceptions;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Services;
using BinDays.Api.Collectors.Telemetry;
using Microsoft.Extensions.Logging;
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

	[Fact]
	public void GetBinDays_WithSomeBinDaysUnmatched_LogsTheCouncilResponseOncePerRequest()
	{
		var address = new Address { Postcode = "AB1 2CD", Uid = "1" };
		var matchedBinDay = new BinDay { Date = new DateOnly(2026, 1, 1), Address = address, Bins = [_generalWaste] };
		var firstUnmatched = new BinDay { Date = new DateOnly(2026, 1, 8), Address = address, Bins = [] };
		var secondUnmatched = new BinDay { Date = new DateOnly(2026, 1, 15), Address = address, Bins = [] };

		var collector = new FakeCollector([firstUnmatched, matchedBinDay, secondUnmatched]);
		var logger = new RecordingLogger<CollectorService>();
		var collectorService = new CollectorService([collector], logger, NullCollectorMetrics.Instance);
		var clientSideResponse = new ClientSideResponse
		{
			RequestId = 1,
			StatusCode = 200,
			ReasonPhrase = "OK",
			Headers = [],
			Content = "Garden Waste Service on 08 January",
		};

		collectorService.GetBinDays(collector.GovUkId, address, clientSideResponse);

		// One response log for the request, however many individual bin days were dropped.
		var responseLogs = logger.Messages.Where(message => message.Contains("Council response follows")).ToList();
		var responseLog = Assert.Single(responseLogs);
		Assert.Contains("Garden Waste Service", responseLog);
	}

	[Fact]
	public void GetBinDays_WithAllBinDaysMatched_DoesNotLogTheCouncilResponse()
	{
		var address = new Address { Postcode = "AB1 2CD", Uid = "1" };
		var matchedBinDay = new BinDay { Date = new DateOnly(2026, 1, 1), Address = address, Bins = [_generalWaste] };

		var collector = new FakeCollector([matchedBinDay]);
		var logger = new RecordingLogger<CollectorService>();
		var collectorService = new CollectorService([collector], logger, NullCollectorMetrics.Instance);
		var clientSideResponse = new ClientSideResponse
		{
			RequestId = 1,
			StatusCode = 200,
			ReasonPhrase = "OK",
			Headers = [],
			Content = "General Waste on 01 January",
		};

		collectorService.GetBinDays(collector.GovUkId, address, clientSideResponse);

		Assert.DoesNotContain(logger.Messages, message => message.Contains("Council response follows"));
	}

	/// <summary>
	/// An <see cref="ILogger{TCategoryName}"/> that captures the messages written to it.
	/// </summary>
	/// <remarks>
	/// Every member here reads instance state, and that is load bearing rather than incidental.
	/// A member that returns a constant and touches no instance state raises the "mark as static"
	/// analyzer suggestion, which this repository's auto-format workflow applies automatically. A
	/// static member cannot implement an interface, so the result does not compile, and it has
	/// broken the build twice: once on ordinary members, and again after they were rewritten as
	/// explicit implementations, where the workflow produced the even more invalid
	/// "static IDisposable? ILogger.BeginScope". Reading instance state is the only form the
	/// suggestion is never raised against. Keep it that way, and do not simplify these into
	/// constant returns.
	/// </remarks>
	private sealed class RecordingLogger<T> : ILogger<T>
	{
		/// <summary>
		/// Every formatted message written, in order.
		/// </summary>
		public List<string> Messages { get; } = [];

		/// <summary>
		/// Every scope opened, in order. Kept so <see cref="BeginScope"/> has instance state to
		/// read, and useful in its own right if a scope ever needs asserting on.
		/// </summary>
		public List<string> Scopes { get; } = [];

		/// <summary>
		/// The lowest level this logger reports as enabled.
		/// </summary>
		public LogLevel MinimumLevel { get; init; } = LogLevel.Trace;

		/// <inheritdoc/>
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull
		{
			Scopes.Add(state.ToString() ?? string.Empty);
			return null;
		}

		/// <inheritdoc/>
		public bool IsEnabled(LogLevel logLevel) => logLevel >= MinimumLevel;

		/// <inheritdoc/>
		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			Messages.Add(formatter(state, exception));
		}
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
