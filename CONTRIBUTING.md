# Contributing

## Too Long; Didn't Read.

- Contributions are welcome.
- New collectors should be implemented:
  - In the same style as existing collectors.
  - With an associated integration test.
- Commit messages should be helpful.
- Don't commit AI slop.

## Overview

The BinDays-API is a relatively simple ASP.Net Core API, with some basic endpoints which call the appropriate methods on the selected collector implementation. It is state-less and has no external dependencies, only providing the requests configuration and processing for the client.

## Development

The following section describes how to begin development with the BinDays-API. When developing the API, such as adding new councils, it is recommended to additionally validate with the BinDays-Client as the integration test client is not a perfect 1:1 match.

### Pre-Requisites

To begin development on the BinDays-API, the following pre-requisites are required:

- Visual Studio or VSCode
- ASP.Net Core SDKs

### Adding a Council

Unlike some other bin collection implementations available, the BinDays-API does not use browser simulation such as Selenium. This is primarily to both significantly improve performance and reduce the compute requirements.

The added cost of this is that it makes implementing a new council more complex, especially when cookies and tokens are involved.

#### Existing Implementations

Before diving in to web scraping, reverse engineering, and beginning to write the implementation for you new council, take the time to look at the various existing implementations. These should be used as a template and reference when adding your own implementation.

All collectors have associated integration tests, which you can use to debug and understand how they work and process the incoming responses from the client.

#### Collector Files

There are two files associated with each collector, the collector itself and the associated integration test. See templates for each below, once implemented the collector will automatically be available for use

##### Collector Implementation Template

```c#
namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Collector implementation for My New Council.
	/// </summary>
	internal sealed partial class MyNewCouncil : GovUkCollectorBase, ICollector
	{
		/// <inheritdoc/>
		public string Name => "My New Council";

		/// <inheritdoc/>
		public Uri WebsiteUrl => new("https://www.mynewcouncil.gov.uk/");

		/// <inheritdoc/>
		public override string GovUkId => "my-new-council";

		/// <summary>
		/// The list of bin types for this collector.
		/// </summary>
		private readonly ReadOnlyCollection<Bin> binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Human Readable Bin Name",
				Colour = "Grey",
				Keys = new List<string>() { "BIN_ID_IN_DATA" }.AsReadOnly(),
			},
		}.AsReadOnly();

		/// <summary>
		/// Regex for the addresses from the data.
		/// </summary>
		[GeneratedRegex(@"")]
		private static partial Regex AddressRegex();

		/// <summary>
		/// Regex for the bin days from the data.
		/// </summary>
		[GeneratedRegex(@"")]
		private static partial Regex BinDaysRegex();

		/// <inheritdoc/>
		public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting addresses
			if (clientSideResponse.RequestId == null)
			{
				// ...
			}
			// Process addresses from response
			else if (clientSideResponse.RequestId == 2)
			{
				// ...
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}

		/// <inheritdoc/>
		public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
		{
			// Prepare client-side request for getting bin days
			if (clientSideResponse.RequestId == null)
			{
				// ...
			}
			// Process bin days from response
			else if (clientSideResponse.RequestId == 2)
			{
				// ...
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}
	}
}
```

##### Collector Integration Test Template

```c#
namespace BinDays.Api.IntegrationTests.Collectors.Councils
{
	using BinDays.Api.Collectors.Collectors;
	using BinDays.Api.Collectors.Collectors.Councils;
	using BinDays.Api.Collectors.Services;
	using BinDays.Api.IntegrationTests.Helpers;
	using System.Threading.Tasks;
	using Xunit;
	using Xunit.Abstractions;

	public class MyNewCouncilTests
	{
		private readonly IntegrationTestClient _client = new();
		private static readonly ICollector _collector = new MyNewCouncil();
		private readonly CollectorService _collectorService = new([_collector]);
		private const string _postcode = "ABCD EFG";

		private readonly ITestOutputHelper _outputHelper;

		public MyNewCouncilTests(ITestOutputHelper outputHelper)
		{
			_outputHelper = outputHelper;
		}

		[Fact]
		public async Task GetBinDaysTest()
		{
			await TestSteps.EndToEnd(
				_client,
				_collectorService,
				_collector,
				_postcode,
				_outputHelper
			);
		}
	}
}
```

### Testing a Council

Once you have written your new council, you will want to test it, often it is unlikely it works first time due to the complexity of many councils.

To do this, either run the tests via your editor such as Visual Studio or VSCode, or instead you can run the following command.

```bash
dotnet test --logger "console;verbosity=detailed" BinDays.Api.IntegrationTests/BinDays.Api.IntegrationTests.csproj --filter MyNewCouncil
```

### Docker Deployment

The BinDays-API is easy to deploy using Docker for testing and development. You can use the following command to build the container, or instead use the public image created automatically from this repository.

```bash
docker build -t bindays-api:latest .
```

You can deploy the public Docker image using the following command.

```bash
docker run -d \
    --name bindays-api \
    -p 8080:8080 \
    ghcr.io/badgerhobbs/bindays-api:latest
```

### Logging

For additional logging, [Seq](https://datalust.co/seq) can be optionally configured. See the [official docs](https://docs.datalust.co/docs/microsoft-extensions-logging#json-configuration) for how configuration steps.
