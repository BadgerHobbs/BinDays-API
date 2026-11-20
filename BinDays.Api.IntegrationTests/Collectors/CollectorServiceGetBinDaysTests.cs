namespace BinDays.Api.IntegrationTests.Collectors;

using BinDays.Api.Collectors.Collectors;
using BinDays.Api.Collectors.Exceptions;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Services;
using BinDays.Api.Collectors.Telemetry;
using BinDays.Api.Collectors.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

/// <summary>
/// Tests that <see cref="CollectorService.GetBinDays"/> reports every council service that matched
/// no bin type, however the collector handles it, and separates a partial or total mismatch from a
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

	private static readonly Address _address = new() { Postcode = "AB1 2CD", Uid = "1" };

	private static readonly DateOnly _firstDate = new(2026, 1, 1);

	private static readonly DateOnly _secondDate = new(2026, 1, 8);

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void GetBinDays_WithUnmatchedServiceOnItsOwnDate_DropsItAndReturnsTheRest(bool skipUnmatched)
	{
		var collector = new FakeCollector([(_firstDate, "General waste"), (_secondDate, "Plastic bags")], skipUnmatched);
		var metrics = new Mock<ICollectorMetrics>();
		var collectorService = new CollectorService([collector], NullLogger<CollectorService>.Instance, metrics.Object);

		var result = collectorService.GetBinDays(collector.GovUkId, _address, null);

		var resultBinDay = Assert.Single(result.BinDays!);
		Assert.Equal(_firstDate, resultBinDay.Date);
		metrics.Verify(recorder => recorder.RecordBinDaysUnmatched(collector.GovUkId, 1), Times.Once);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void GetBinDays_WithUnmatchedServiceOnAMatchedDate_StillReportsIt(bool skipUnmatched)
	{
		var collector = new FakeCollector([(_firstDate, "General waste"), (_firstDate, "Plastic bags")], skipUnmatched);
		var metrics = new Mock<ICollectorMetrics>();
		var (logger, messages) = CreateLoggerMock();
		var collectorService = new CollectorService([collector], logger.Object, metrics.Object);

		var result = collectorService.GetBinDays(collector.GovUkId, _address, null);

		var resultBinDay = Assert.Single(result.BinDays!);
		Assert.Equal([_generalWaste], resultBinDay.Bins);
		metrics.Verify(recorder => recorder.RecordBinDaysUnmatched(collector.GovUkId, 1), Times.Once);
		Assert.Contains(messages, message => message.StartsWith("Service \"Plastic bags\" for gov.uk ID: fake-collector"));
	}

	[Fact]
	public void GetBinDays_WithAServiceUnmatchedOnSeveralDates_LogsItOnceAndCountsEachOccurrence()
	{
		var collector = new FakeCollector([
			(_firstDate, "General waste"),
			(_firstDate, "Plastic bags"),
			(_secondDate, "Plastic bags"),
			(_secondDate, "Textiles"),
		]);
		var metrics = new Mock<ICollectorMetrics>();
		var (logger, messages) = CreateLoggerMock();
		var collectorService = new CollectorService([collector], logger.Object, metrics.Object);

		collectorService.GetBinDays(collector.GovUkId, _address, null);

		metrics.Verify(recorder => recorder.RecordBinDaysUnmatched(collector.GovUkId, 3), Times.Once);
		Assert.Single(messages, message => message.StartsWith("Service \"Plastic bags\"") && message.EndsWith("(2 occurrence(s))."));
		Assert.Single(messages, message => message.StartsWith("Service \"Textiles\"") && message.EndsWith("(1 occurrence(s))."));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void GetBinDays_WithAllServicesUnmatched_ThrowsAllBinDaysUnmatchedException(bool skipUnmatched)
	{
		var collector = new FakeCollector([(_firstDate, "Plastic bags")], skipUnmatched);
		var collectorService = new CollectorService([collector], NullLogger<CollectorService>.Instance, Mock.Of<ICollectorMetrics>());

		Assert.Throws<AllBinDaysUnmatchedException>(() => collectorService.GetBinDays(collector.GovUkId, _address, null));
	}

	[Fact]
	public void GetBinDays_WithNoBinDays_ThrowsBinDaysNotFoundException()
	{
		var collector = new FakeCollector([]);
		var collectorService = new CollectorService([collector], NullLogger<CollectorService>.Instance, Mock.Of<ICollectorMetrics>());

		Assert.Throws<BinDaysNotFoundException>(() => collectorService.GetBinDays(collector.GovUkId, _address, null));
	}

	[Fact]
	public void GetBinDays_WithAllServicesMatched_RecordsNoMetric()
	{
		var collector = new FakeCollector([(_firstDate, "General waste")]);
		var metrics = new Mock<ICollectorMetrics>();
		var collectorService = new CollectorService([collector], NullLogger<CollectorService>.Instance, metrics.Object);

		collectorService.GetBinDays(collector.GovUkId, _address, null);

		metrics.Verify(recorder => recorder.RecordBinDaysUnmatched(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
	}

	[Fact]
	public void GetBinDays_AfterALookupWithUnmatchedServices_StartsTheNextLookupClean()
	{
		var unmatchedCollector = new FakeCollector([(_firstDate, "General waste"), (_firstDate, "Plastic bags")], govUkId: "unmatched-collector");
		var matchedCollector = new FakeCollector([(_firstDate, "General waste")], govUkId: "matched-collector");
		var metrics = new Mock<ICollectorMetrics>();
		var collectorService = new CollectorService([unmatchedCollector, matchedCollector], NullLogger<CollectorService>.Instance, metrics.Object);

		collectorService.GetBinDays(unmatchedCollector.GovUkId, _address, null);
		collectorService.GetBinDays(matchedCollector.GovUkId, _address, null);

		metrics.Verify(recorder => recorder.RecordBinDaysUnmatched(matchedCollector.GovUkId, It.IsAny<int>()), Times.Never);
	}

	[Fact]
	public void GetBinDays_WithAllServicesMatched_DoesNotLogTheCouncilResponse()
	{
		var collector = new FakeCollector([(_firstDate, "General waste")]);
		var (logger, messages) = CreateLoggerMock();
		var collectorService = new CollectorService([collector], logger.Object, Mock.Of<ICollectorMetrics>());
		var clientSideResponse = new ClientSideResponse
		{
			RequestId = 1,
			StatusCode = 200,
			ReasonPhrase = "OK",
			Headers = [],
			Content = "General Waste on 01 January",
		};

		collectorService.GetBinDays(collector.GovUkId, _address, clientSideResponse);

		Assert.DoesNotContain(messages, message => message.Contains("Council response follows"));
	}

	/// <summary>
	/// Builds an <see cref="ILogger{TCategoryName}"/> mock together with the list its formatted
	/// messages land in.
	/// </summary>
	/// <remarks>
	/// ILogger.Log is generic in its state parameter, which Moq cannot target with a normal typed
	/// setup; It.IsAnyType plus a reflection-based callback is the documented way around that. The
	/// formatter delegate is exactly what the real logging extension methods (LogWarning etc.)
	/// build internally, so invoking it here reconstructs the same message a real logger would
	/// have written.
	/// </remarks>
	private static (Mock<ILogger<CollectorService>> Logger, List<string> Messages) CreateLoggerMock()
	{
		var messages = new List<string>();
		var logger = new Mock<ILogger<CollectorService>>();

		logger
			.Setup(target => target.Log(
				It.IsAny<LogLevel>(),
				It.IsAny<EventId>(),
				It.IsAny<It.IsAnyType>(),
				It.IsAny<Exception?>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
			.Callback(new InvocationAction(invocation =>
			{
				var state = invocation.Arguments[2];
				var exception = (Exception?)invocation.Arguments[3];
				var formatter = invocation.Arguments[4];
				var invoke = formatter.GetType().GetMethod("Invoke")!;
				messages.Add((string)invoke.Invoke(formatter, [state, exception])!);
			}));

		return (logger, messages);
	}

	/// <summary>
	/// A minimal <see cref="ICollector"/> stub that maps a fixed list of council services to bin
	/// days the way real collectors do, with no client-side requests required.
	/// </summary>
	/// <remarks>
	/// Real collectors handle a service that matches no bin type in one of two ways: some keep an
	/// empty bin day, which <see cref="ProcessingUtilities.MergeBinDays"/> then absorbs into any
	/// matched bin day on the same date, and others skip it outright. Both are covered here.
	/// </remarks>
	private sealed class FakeCollector : ICollector
	{
		private readonly IReadOnlyCollection<(DateOnly Date, string Service)> _services;

		private readonly bool _skipUnmatched;

		public FakeCollector(
			IReadOnlyCollection<(DateOnly Date, string Service)> services,
			bool skipUnmatched = false,
			string govUkId = "fake-collector")
		{
			_services = services;
			_skipUnmatched = skipUnmatched;
			GovUkId = govUkId;
		}

		public string Name { get; } = "Fake Collector";

		public Uri WebsiteUrl { get; } = new("https://example.com");

		public string GovUkId { get; }

		public Uri GovUkUrl { get; } = new("https://www.gov.uk/find-out-when-your-bin-collection-day-is/fake-collector");

		public int Version { get; } = 1;

		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse) =>
			throw new NotImplementedException();

		public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
		{
			var binDays = new List<BinDay>();
			foreach (var (date, service) in _services)
			{
				var matchedBins = ProcessingUtilities.GetMatchingBins([_generalWaste], service);

				if (_skipUnmatched && matchedBins.Count == 0)
				{
					continue;
				}

				binDays.Add(new BinDay { Date = date, Address = address, Bins = matchedBins });
			}

			return new() { BinDays = ProcessingUtilities.MergeBinDays(binDays) };
		}
	}
}
