namespace BinDays.Api.IntegrationTests.Collectors;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Exceptions;
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Services;
using BinDays.Api.Collectors.Telemetry;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

public sealed class InvalidPostcodeTests
{
	[Fact]
	public void GetCollector_WithInvalidPostcodeResponse_ThrowsInvalidPostcodeException()
	{
		var collectorService = new CollectorService([], NullLogger<CollectorService>.Instance, Mock.Of<ICollectorMetrics>());

		var clientSideResponse = new ClientSideResponse
		{
			RequestId = 1,
			StatusCode = 200,
			Headers = [],
			Content =
				"""
				<html>
					<body>
						<div>This isn&#39;t a valid postcode.</div>
					</body>
				</html>
				""",
			ReasonPhrase = "OK"
		};

		var exception = Assert.Throws<InvalidPostcodeException>(
			() => GovUkCollectorBase.GetCollector(collectorService, "INVALID", clientSideResponse)
		);

		Assert.Equal("INVALID", exception.Postcode);
	}
}
