namespace BinDays.Api.IntegrationTests.Collectors;

using BinDays.Api.Collectors.Collectors;
using BinDays.Api.Collectors.Exceptions;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Services;
using BinDays.Api.Collectors.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
		var collectorService = new CollectorService([collector], NullLogger<CollectorService>.Instance, Mock.Of<ICollectorMetrics>());

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
		var collectorService = new CollectorService([collector], NullLogger<CollectorService>.Instance, Mock.Of<ICollectorMetrics>());

		Assert.Throws<AllBinDaysUnmatchedException>(() => collectorService.GetBinDays(collector.GovUkId, address, null));
	}

	[Fact]
	public void GetBinDays_WithNoBinDays_ThrowsBinDaysNotFoundException()
	{
		var address = new Address { Postcode = "AB1 2CD", Uid = "1" };

		var collector = new FakeCollector([]);
		var collectorService = new CollectorService([collector], NullLogger<CollectorService>.Instance, Mock.Of<ICollectorMetrics>());

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
		var metrics = new Mock<ICollectorMetrics>();
		var collectorService = new CollectorService([collector], NullLogger<CollectorService>.Instance, metrics.Object);

		collectorService.GetBinDays(collector.GovUkId, address, null);

		metrics.Verify(recorder => recorder.RecordBinDayUnmatched(collector.GovUkId), Times.Exactly(2));
	}

	[Fact]
	public void GetBinDays_WithAllBinDaysMatched_RecordsNoMetric()
	{
		var address = new Address { Postcode = "AB1 2CD", Uid = "1" };
		var matchedBinDay = new BinDay { Date = new DateOnly(2026, 1, 1), Address = address, Bins = [_generalWaste] };

		var collector = new FakeCollector([matchedBinDay]);
		var metrics = new Mock<ICollectorMetrics>();
		var collectorService = new CollectorService([collector], NullLogger<CollectorService>.Instance, metrics.Object);

		collectorService.GetBinDays(collector.GovUkId, address, null);

		metrics.Verify(recorder => recorder.RecordBinDayUnmatched(It.IsAny<string>()), Times.Never);
	}

	[Fact]
	public void GetBinDays_WithSomeBinDaysUnmatched_LogsTheCouncilResponseOncePerRequest()
	{
		var address = new Address { Postcode = "AB1 2CD", Uid = "1" };
		var matchedBinDay = new BinDay { Date = new DateOnly(2026, 1, 1), Address = address, Bins = [_generalWaste] };
		var firstUnmatched = new BinDay { Date = new DateOnly(2026, 1, 8), Address = address, Bins = [] };
		var secondUnmatched = new BinDay { Date = new DateOnly(2026, 1, 15), Address = address, Bins = [] };

		var collector = new FakeCollector([firstUnmatched, matchedBinDay, secondUnmatched]);
		var (logger, messages) = CreateLoggerMock();
		var collectorService = new CollectorService([collector], logger.Object, Mock.Of<ICollectorMetrics>());
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
		var responseLogs = messages.Where(message => message.Contains("Council response follows")).ToList();
		var responseLog = Assert.Single(responseLogs);
		Assert.Contains("Garden Waste Service", responseLog);
	}

	[Fact]
	public void GetBinDays_WithAllBinDaysMatched_DoesNotLogTheCouncilResponse()
	{
		var address = new Address { Postcode = "AB1 2CD", Uid = "1" };
		var matchedBinDay = new BinDay { Date = new DateOnly(2026, 1, 1), Address = address, Bins = [_generalWaste] };

		var collector = new FakeCollector([matchedBinDay]);
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

		collectorService.GetBinDays(collector.GovUkId, address, clientSideResponse);

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
