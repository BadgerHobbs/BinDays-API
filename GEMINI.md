# Project: BinDays API

This document provides instructions and guidelines for contributing to the BinDays API, specifically for adding new council collectors.

## General Instructions:

- Your primary task is to add support for a new council to the BinDays API.
- You must follow the existing coding style, conventions, and architectural patterns.
- All new collectors require a corresponding integration test.
- All HTML parsing must be done using regular expressions, as is the convention in this repository.
- **Do not modify core files.** Your changes should be limited to creating one new collector class and one new integration test file. Do not alter any other part of the existing codebase.

## Guiding Principles

- **Reference Existing Work:** Before writing any code, thoroughly review the existing council implementations in `BinDays.Api.Collectors/Collectors/Councils/`. This is crucial for maintaining consistency and identifying reusable patterns. Many councils use similar back-end systems, so you may find an existing collector that is very close to what is needed for the new council.
- **Use Playwright for Web Interaction:** All website interactions must be performed using the Playwright MCP server. Do not use direct HTTP request tools like `curl` or `view_text_website`. Your investigation should be based on browser automation with Playwright.
- **Temporary Debugging:** You are encouraged to add temporary print/debug statements (e.g., `Console.WriteLine`) to your code to output HTML, JSON, or other data to understand the responses you are working with. However, you **must** remove all such temporary statements before you consider the task complete.

## Workflow for Adding a New Council Collector:

1.  **Initial Research:**
    - Using Playwright, navigate to the UK government's bin collection page: `https://www.gov.uk/rubbish-collection-day`.
    - Enter the example postcode for the new council.
    - Record the exact council name and the `GovUkId` from the resulting URL.
2.  **Website Investigation (Playwright):**
    - Follow the link to the council's official website.
    - Using Playwright, script the user journey to find the bin collection schedule:
        a. Navigate to the bin/waste collection page.
        b. Enter the example postcode and search.
        c. Select the first address from the results.
        d. View the upcoming bin collection schedule.
    - During this process, record the network requests made by the browser. Pay close attention to the specific requests that fetch address data and bin collection data. Note their URLs, headers, and payloads. This information is critical for implementation.
3.  **Implementation:**
    - Create a new C# class for the council in `BinDays.Api.Collectors/Collectors/Councils/`.
    - The class must inherit from `GovUkCollectorBase` and implement `ICollector`.
    - Use the captured HTTP request data to implement the `GetAddresses` and `GetBinDays` methods.
4.  **Testing:**
    - Create a new integration test file in `BinDays.Api.IntegrationTests/Collectors/Councils/`.
    - The test must successfully retrieve bin collection data for the example postcode using the `TestSteps.EndToEnd` helper.
    - Run the test and ensure it passes.

## Collector Implementation Template:

```c#
namespace BinDays.Api.Collectors.Collectors.Councils
{
	using BinDays.Api.Collectors.Models;
	using BinDays.Api.Collectors.Utilities;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	.Text.RegularExpressions;

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

## Integration Test Template:

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
