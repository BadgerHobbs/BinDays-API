# BinDays-API

[![Integration Tests](https://github.com/BadgerHobbs/BinDays-API/actions/workflows/integration-tests.yml/badge.svg)](https://github.com/BadgerHobbs/BinDays-API/actions/workflows/integration-tests.yml) [![Build and Push Image](https://github.com/BadgerHobbs/BinDays-API/actions/workflows/build-and-push-image.yml/badge.svg)](https://github.com/BadgerHobbs/BinDays-API/actions/workflows/build-and-push-image.yml) [![License: AGPL-3.0](https://img.shields.io/badge/License-AGPL_3.0-blue.svg)](https://opensource.org/licenses/AGPL-3.0)

![d2(4)](https://github.com/user-attachments/assets/8a7d9784-08fe-4946-a58e-3ed36fedc54b)

## FAQs

Below are some frequently asked questions which you may have, please read through them before creating tickets or raising issues.

### I would like to request a new council is added

Firstly, please check that both the council you seek to get added is not already supported, undergoing development, or already requested. Search the existing GitHub issues as this may be the case.

If there is no pre-existing issue for the specified council, please fill in the council request form, making sure to complete the template.

Please be aware that there is no guarantees that this council gets implemented and has long-term support.

### I would like to report an issue with an existing council

If you have found an issue with an existing council, please create a GitHub issue, making sure to complete the template.

### I have suggestions for improvements for the mobile app

This repository is for the BinDays-API, which is the server-side component of the BinDays project. If you have suggestions for improvements for the mobile app, please create an issue in the BinDays-App repository.

### I would like to report an issue with the mobile app

Please create an issue in the BinDays-App repository.

## Overview

You are currently viewing the repository for the BinDays-API, with is the server-side component of the BinDays project that provides both the requests that the clients must make plus the processing of their responses.

At a high-level, all the BinDays-API does is enable requests for bin collection councils to be configured and processed server-side, while executed client-side. The main advantages of this approaches are as follows:

- New councils can be added and existing councils can be updated without requiring changes client-side, such as in the BinDays app.
- Avoids rate-limiting, captchas, and IP blocking often caused by making many requests from a single source on a non-residential IP address.

### Low-Level Design

At a low-level, the BinDays-API is structured around five core requests/methods which are used across all collector implementations. These are:

- `ClientSideRequest` a request to be made by the client.
- `ClientSideResponse` a response returned by the client.
- `GetCollectorResponse` a response containing the next request to be made (if required) and the collector (if found).
- `GetAddressesResponse` a response containing the next request to be made (if required) and the addresses (if found).
- `GetBinDaysResponse` a response containing the next request to be made (if required) and the bin days (if found).

For the above requests/responses, each collector implements `ICollector` and `GovUkCollectorBase` which contain three main methods:

- `GetCollector` takes in the postcode and optional client-side response. It returns either the collector or the next request to make depending on internal logic.
- `GetAddresses` takes in the postcode and optional client-side response. It returns either the addresses or the next request to make depending on internal logic.
- `GetBinDays` takes in the address and optional client-side response. It returns either the bin days or the next request to make depending on internal logic.

While it is generally standard that the client makes two requests to each endpoint, one for the initial next request and the other to send the response for processing, for some collectors this can be more if they require data such as cookies or tokens.

## Development

The following section describes how to begin deveopment with the BinDays-API. When developing the API, such as adding new councils, it is reccomended to validate with the BinDays-Client as the integration test client is not a perfect 1:1 match.

### Overview

The BinDays-API is a relatively simple ASP.Net Core API, with some basic endpoints which call the appropriate methods on the selected collector implementation. It is state-less and has no external dependencies, only providing the requests configuration and processing for the client.

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

### Pre-Requisites

To begin development on the BinDays-API, the following pre-requisites are required:

- Visual Studio or VSCode
- ASP.Net Core SDKs

### Adding a Council

Unlike some other bin collection implementations available, the BinDays-API does not use browser simulation such as Selenium. This is primarily to both significantly improve performance and reduce the compute requirements. The added cost of this is that it makes implementing a new council more complex, especially when cookies and tokens are involved.

#### Existing Implementations

Before diving in to web scraping, reverse engineering, and beginning to write the implementation for you new council, take the time to look at the various existing implementations. These should be used as a template and reference when adding your own implementation.

All collectors have associated integration tests, which you can use to debug and understand how they work and process the incoming responses from the client.

#### Collector Files

There are two files associated with each collector, the collector itself and the associated integration test. See templates for each below, once implemented the collector will automatically be avaialble for use.

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

## Contributing

If you would like to contribute to the development of the BinDays-API, please fork the repository and submit a pull request.

Before submitting a pull request, please ensure that:

- Your code follows the existing code style.
- All tests pass.

## License

The code and documentation in this project are released under the [AGPL-3.0 License](LICENSE).
