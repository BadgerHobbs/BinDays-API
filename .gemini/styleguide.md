# BinDays-API C# Style Guide

## Introduction

This style guide outlines the coding conventions for C# code developed for the BinDays-API project. It's intended to ensure that all contributions are consistent, readable, and maintainable.

## Key Principles

- **Readability:** Code should be easy to understand for all team members.
- **Maintainability:** Code should be easy to modify and extend.
- **Consistency:** Adhering to a consistent style across all projects improves collaboration and reduces errors.
- **No Browser Emulation:** Do not use browser emulation tools like Selenium. Instead, replicate the necessary HTTP requests directly. This keeps the collectors lightweight and avoids heavy dependencies.

## File Structure

- **New Collectors:** Place new council collector classes in `BinDays.Api.Collectors/Collectors/Councils/`. The filename should match the class name (e.g., `MyNewCouncil.cs`).
- **Integration Tests:** Corresponding integration tests for new collectors must be placed in `BinDays.Api.IntegrationTests/Collectors/Councils/`. The test filename should be `[CollectorName]Tests.cs` (e.g., `MyNewCouncilTests.cs`).

## Naming Conventions

- **Collector Classes:** Use PascalCase (e.g., `MyNewCouncil`).
- **Interfaces:** Use the `I` prefix (e.g., `ICollector`).
- **Methods and Properties:** Use PascalCase (e.g., `GetAddresses`, `WebsiteUrl`).
- **Private Fields:** Use `_camelCase` (e.g., `_client`).

## Collector Implementation and Design

This section covers the details of how to implement a collector, including the required interface, the core design philosophy, and data handling best practices.

### Core Requirements

All collectors must implement the `ICollector` interface.

A collector must implement the following properties and methods:

- `Name`: The human-readable name of the council.
- `WebsiteUrl`: The `Uri` of the council's website.
- `GovUkId`: The council's identifier on gov.uk.
- `GovUkUrl`: The `Uri` for the council on gov.uk.
- `GetAddresses(string postcode, ClientSideResponse? clientSideResponse)`: The method to retrieve addresses for a given postcode.
- `GetBinDays(Address address, ClientSideResponse? clientSideResponse)`: The method to retrieve bin collection days for a given address.

### Design Philosophy

Collectors in this project follow a few specific design principles that are important to understand when contributing.

- **Stateless by Design:** Because all requests to council websites originate from the client application (the user's device), the API itself is stateless. This means you cannot save state, such as authentication tokens or session cookies, between the different steps of a collector's process (e.g., between `GetAddresses` and `GetBinDays`). Each step is an independent transaction.

- **Intentionally Brittle and Minimal Exception Handling:** Collectors are intentionally designed to be "brittle"—that is, they are expected to fail loudly and quickly if the council's website changes or if the data format is not what is expected.

  - **Avoid `try/catch` blocks:** Do not wrap parsing logic in `try/catch` blocks to handle nulls or formatting issues silently. Let the code raise exceptions (e.g., `NullReferenceException`, `FormatException`).
  - **Why?** This approach ensures that errors are not hidden. When a collector fails, the error is captured and logged at a higher level in the application. This makes it immediately obvious that a collector is broken and provides a clear stack trace, which makes debugging and fixing the issue much easier. The multi-stage process of each collector helps pinpoint exactly where the failure occurred. You can see this pattern in existing council implementations, which have very little explicit exception handling.
  - **Custom Exceptions:** For predictable, high-level failures (e.g., a postcode not being found in the `gov.uk` service), create and throw a custom exception (e.g., `GovUkIdNotFoundException`) to provide more specific error context.

- **Code Reuse with Base Classes:** If multiple collectors share a significant amount of logic (e.g., interacting with the same third-party service like `gov.uk`), encapsulate the shared logic in a base class to promote code reuse and maintainability.

### Data Handling and Parsing

- **Parsing Strategy:**

  - **For HTML:** Prefer using regular expressions (`Regex`) for extracting data. This keeps dependencies minimal.
  - **For JSON:** Use the built-in `System.Text.Json` library. When using `JsonDocument`, ensure it is properly disposed of with a `using` statement to manage memory effectively.

- **Robust Date Parsing:** Always use `DateOnly.ParseExact` or `DateTime.ParseExact` with `CultureInfo.InvariantCulture` when parsing dates from strings. This prevents issues caused by different server locales or date formats.

- **Data Cleaning:** Always `Trim()` strings retrieved from external sources to remove leading/trailing whitespace.

- **Handling Secrets:** Store API keys or other secrets as `private const string` fields within the collector class. Do not expose them publicly.

- **Flexible Bin Matching:** The `_binTypes` collection allows for flexible matching of bin types from the source data. Use the `Keys` property to define one or more identifiers from the data that map to a specific bin. Matching logic can be case-insensitive or based on partial strings as needed.

## Code Examples

This section provides complete code templates for a new collector and its corresponding integration test.

### Example Collector Template

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
		private readonly ReadOnlyCollection<Bin> _binTypes = new List<Bin>()
		{
			new()
			{
				Name = "Human Readable Bin Name",
				Colour = BinColor.Grey,
				Keys = new List<string>() { "BIN_ID_IN_DATA" }.AsReadOnly(),
				Type = BinType.Bag,
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
				var binDays = new List<BinDay>();

				// ...

				var getBinDaysResponse = new GetBinDaysResponse()
				{
					BinDays = ProcessingUtilities.ProcessBinDays(binDays),
				};

				return getBinDaysResponse;
			}

			// Throw exception for invalid request
			throw new InvalidOperationException("Invalid client-side request.");
		}
	}
}
```

### Example Integration Test Template

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
		private readonly ITestOutputHelper _outputHelper;

		public MyNewCouncilTests(ITestOutputHelper outputHelper)
		{
			_outputHelper = outputHelper;
		}

		[Theory]
		[InlineData("ABCD EFG")]
		public async Task GetBinDaysTest(string postcode)
		{
			await TestSteps.EndToEnd(
				_client,
				_collectorService,
				_collector,
				postcode,
				_outputHelper
			);
		}
	}
}
```

## Testing and Committing

After implementing your collector and its integration test, follow these final steps.

### Integration Tests

- **Requirement:** Every new collector **must** have an accompanying integration test.
- **Test Helper:** Use the `TestSteps.EndToEnd` helper for end-to-end testing of the collector.
- **Test Naming:** Test methods should be named descriptively (e.g., `GetBinDaysTest`).

### Commit Messages

- **Be Descriptive:** Write clear and descriptive commit messages that explain the "what" and "why" of the changes.
- **Reference Issues:** If your commit addresses an issue, reference it in the commit message (e.g., `Fixes #123`).
