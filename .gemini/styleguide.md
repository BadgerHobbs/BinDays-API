# BinDays-API C# Style Guide

## Introduction

This style guide outlines the coding conventions for C# code developed for the BinDays-API project. It's intended to ensure that all contributions are consistent, readable, and maintainable.

## Key Principles

- **Readability:** Code should be easy to understand for all team members.
- **Maintainability:** Code should be easy to modify and extend.
- **Consistency:** Adhering to a consistent style across all projects improves collaboration and reduces errors.
- **No Browser Emulation:** Do not use browser emulation tools like Selenium. Instead, replicate the necessary HTTP requests directly. This keeps the collectors lightweight and avoids heavy dependencies.

## File Structure

- **New Collectors:** Place new council collector classes in `BinDays.Api.Collectors/Collectors/Councils/`. The filename should match the class name (e.g. `MyNewCouncil.cs`).
- **Integration Tests:** Corresponding integration tests for new collectors must be placed in `BinDays.Api.IntegrationTests/Collectors/Councils/`. The test filename should be `[CollectorName]Tests.cs` (e.g. `MyNewCouncilTests.cs`).

## Naming Conventions

- **Collector Classes:** Use PascalCase (e.g. `MyNewCouncil`).
- **Interfaces:** Use the `I` prefix (e.g. `ICollector`).
- **Methods and Properties:** Use PascalCase (e.g. `GetAddresses`, `WebsiteUrl`).
- **Private Fields:** Use `_camelCase` (e.g. `_binTypes`, `_client`).

## Property Implementation

- **Expression-Bodied Members:** Always use expression-bodied syntax (`=>`) for property getters, never full property blocks.
- **Documentation:** Always use `/// <inheritdoc/>` for interface properties (`Name`, `WebsiteUrl`, `GovUkId`).
- **Uri Instantiation:** Use target-typed `new("url")` syntax for Uri properties.

Example:
```c#
/// <inheritdoc/>
public string Name => "My New Council";

/// <inheritdoc/>
public Uri WebsiteUrl => new("https://www.mynewcouncil.gov.uk/");

/// <inheritdoc/>
public override string GovUkId => "my-new-council";
```

## Class Declaration

- **Access Modifier:** Always use `internal` for collector classes (not `public`).
- **Sealed:** Always declare collectors as `sealed` to prevent inheritance.
- **Partial:** Use `partial` **only** when the class uses `[GeneratedRegex]` attributes. Standard collectors with regex use `internal sealed partial class`. Vendor base collectors without regex use `internal sealed class`.
- **Inheritance:** All collectors inherit from `GovUkCollectorBase` (directly or through a vendor base class) and explicitly implement `ICollector`.

Examples:
```c#
// Standard collector with regex - requires partial
internal sealed partial class MyNewCouncil : GovUkCollectorBase, ICollector

// Vendor base collector without regex - no partial needed
internal sealed class MyVendorCouncil : ITouchVisionCollectorBase, ICollector
```

## Class Member Ordering

Maintain consistent ordering of class members to improve code readability. Group **configuration** members together, followed by **implementation** members:

### Configuration (in this order):

1. **Interface Properties**:
   - `Name`
   - `WebsiteUrl`
   - `GovUkId` (or vendor-specific overrides for vendor base collectors)

2. **Bin Types**:
   - Standard collectors: `private readonly IReadOnlyCollection<Bin> _binTypes`
   - Vendor collectors: `protected override IReadOnlyCollection<Bin> BinTypes`

3. **Private Const Fields** (if needed):
   - API keys, subscription keys, signatures, etc.
   - Use descriptive names like `_apiKey`, `_apiSubscriptionKey`, `_signature`

### Implementation (in this order):

4. **Regex Methods** (if using `[GeneratedRegex]`):
   - Typical order: `TokenRegex()`, `AddressRegex()`, `BinDaysRegex()`
   - Other helper regexes as needed

5. **Interface Methods**:
   - `GetAddresses()`
   - `GetBinDays()`

### Example structure:
```c#
internal sealed partial class MyCouncil : GovUkCollectorBase, ICollector
{
    // === CONFIGURATION ===

    // Interface properties
    public string Name => "...";
    public Uri WebsiteUrl => new("...");
    public override string GovUkId => "...";

    // Private const fields (if needed)
    private const string _apiKey = "...";

    // Bin types
    private readonly IReadOnlyCollection<Bin> _binTypes = [...];

    // === IMPLEMENTATION ===

    // Regex methods
    [GeneratedRegex(@"...")]
    private static partial Regex AddressRegex();

    // Interface methods
    public GetAddressesResponse GetAddresses(...) { ... }
    public GetBinDaysResponse GetBinDays(...) { ... }
}
```

## Collector Implementation and Design

This section covers the details of how to implement a collector, including the required interface, the core design philosophy, and data handling best practices.

### Core Requirements

All collectors must implement the `ICollector` interface and inherit from either `GovUkCollectorBase` (for custom implementations) or a vendor-specific base class (for councils using shared platforms).

**Standard Collector (inheriting from `GovUkCollectorBase`):**

A collector must implement the following properties and methods:

- `Name`: The human-readable name of the council.
- `WebsiteUrl`: The `Uri` of the council's website.
- `GovUkId`: The council's identifier on gov.uk (marked with `override`).
- `GovUkUrl`: The `Uri` for the council on gov.uk (inherited from base).
- `GetAddresses(string postcode, ClientSideResponse? clientSideResponse)`: The method to retrieve addresses for a given postcode.
- `GetBinDays(Address address, ClientSideResponse? clientSideResponse)`: The method to retrieve bin collection days for a given address.

**Vendor Base Class Collector (e.g., inheriting from `ITouchVisionCollectorBase`):**

These collectors have a simpler implementation and must define:

- `Name`: The human-readable name of the council.
- `WebsiteUrl`: The `Uri` of the council's website.
- `GovUkId`: The council's identifier on gov.uk (marked with `override`).
- `ClientId`: The vendor-specific client identifier (marked with `override`).
- `CouncilId`: The vendor-specific council identifier (marked with `override`).
- `ApiBaseUrl`: The vendor's API base URL (marked with `override`).
- `BinTypes`: The collection of bin types as a property (marked with `override`), not a private field.

Do **not** implement `GetAddresses()` or `GetBinDays()` for vendor base collectors—these are handled by the base class.

### Design Philosophy

Collectors in this project follow a few specific design principles that are important to understand when contributing.

- **Stateless by Design:** Because all requests to council websites originate from the client application (the user's device), the API itself is stateless. This means you cannot save state, such as authentication tokens or session cookies, between the different steps of a collector's process (e.g. between `GetAddresses` and `GetBinDays`). Each step is an independent transaction.

- **Step-by-Step Request Implementation:** For collectors that require multiple client-side requests, use a step-by-step implementation based on the `RequestId` of the `clientSideResponse`. This is typically structured as an `if/else if` chain.

  - **RequestId Numbering:** Always start RequestId at `1` (not `0`) and increment sequentially.
  - **Initial Request Pattern:** Always start with `if (clientSideResponse == null)` for the initial client-side request.
  - **Subsequent Requests:** Use `else if (clientSideResponse.RequestId == X)` for each step in the flow.
  - **Fallthrough Handler:** Always end with `throw new InvalidOperationException("Invalid client-side request.");` to catch invalid states.
  - **Comment Style:** Use clear comments before each block: `// Prepare client-side request for getting X` or `// Process X from response`.
  - **Preserve Existing Logic:** Do not refactor the existing `if/else if` structure. This pattern is intentional and provides a clear, linear flow for debugging and maintaining multi-step processes.

- **Intentionally Brittle and Minimal Exception Handling:** Collectors are intentionally designed to be "brittle"—that is, they are expected to fail loudly and quickly if the council's website changes or if the data format is not what is expected.

  - **Avoid `try/catch` blocks:** Do not wrap parsing logic in `try/catch` blocks to handle nulls or formatting issues silently. Let the code raise exceptions (e.g. `NullReferenceException`, `FormatException`).
  - **Why?** This approach ensures that errors are not hidden. When a collector fails, the error is captured and logged at a higher level in the application. This makes it immediately obvious that a collector is broken and provides a clear stack trace, which makes debugging and fixing the issue much easier. The multi-stage process of each collector helps pinpoint exactly where the failure occurred. You can see this pattern in existing council implementations, which have very little explicit exception handling.
  - **Custom Exceptions:** For predictable, high-level failures (e.g. a postcode not being found in the `gov.uk` service), create and throw a custom exception (e.g. `GovUkIdNotFoundException`) to provide more specific error context.

- **Code Reuse with Base Classes:** If multiple collectors share a significant amount of logic (e.g. interacting with the same third-party service like `gov.uk`), encapsulate the shared logic in a base class to promote code reuse and maintainability.

### Data Handling and Parsing

- **Parsing Strategy:**

  - **For HTML:** Prefer using regular expressions (`Regex`) for extracting data. This keeps dependencies minimal.
    - Always use `[GeneratedRegex]` attribute with static partial methods (e.g., `AddressRegex()`, `BinDaysRegex()`).
    - Use the null-forgiving operator on regex matches: `Matches(content)!` since we expect matches or want failures to propagate.
    - Name methods with PascalCase and "Regex" suffix: `TokenRegex()`, `AddressRegex()`, `BinDaysRegex()`.
    - Use verbatim strings for regex patterns: `@"pattern"`.
    - Use named capture groups: `(?<name>...)` for extracting data.
    - Requires the class to be declared `partial`.

  - **For JSON:** Use the built-in `System.Text.Json` library.
    - When using `JsonDocument`, use `using var` for automatic disposal: `using var jsonDoc = JsonDocument.Parse(content);`
    - Iterate arrays with `EnumerateArray()`: `foreach (var element in jsonDoc.RootElement.EnumerateArray())`
    - Get properties with `GetProperty("name").GetString()`.

- **Robust Date Parsing:**
  - **With Year:** Always use `DateOnly.ParseExact` or `DateTime.ParseExact` with explicit format strings and `CultureInfo.InvariantCulture`:
    ```c#
    var date = DateOnly.ParseExact(
        dateString,
        "dd/MM/yyyy",
        CultureInfo.InvariantCulture,
        DateTimeStyles.None
    );
    ```
  - **Without Year:** When the source data lacks a year, use the `ParseDateInferringYear()` extension method (see Common Utilities section below).

- **Data Cleaning:**
  - Always `Trim()` strings retrieved from external sources to remove leading/trailing whitespace.
  - Filter out placeholder values explicitly: `if (uid == "-1" || uid == "111111") { continue; }`

- **Handling Secrets:** Store API keys or other secrets as `private const string` fields within the collector class. Do not expose them publicly.

- **Flexible Bin Matching:**
  - Use `ProcessingUtilities.GetMatchingBins(_binTypes, sourceKey)` to match bins by their keys.
  - The `_binTypes` collection allows for flexible matching—use the `Keys` property to define one or more identifiers.
  - Matching logic can be case-insensitive or based on partial strings as needed.

- **Model Defaults:** To simplify object creation and reduce redundant code, model properties have default values where applicable.

  - `ClientSideRequest.Headers`: Defaults to an empty `Dictionary<string, string>`.
  - `ClientSideRequest.Body`: Defaults to `null`.
  - `NextClientSideRequest`: The `NextClientSideRequest` property in response models like `GetAddressesResponse` and `GetBinDaysResponse` defaults to `null`.
  - **Avoid Redundant Initializations:** When creating new instances of these models, do not explicitly set these properties to their default values (e.g. avoid `Headers = []` or `NextClientSideRequest = null`).

- **Enums for Bins:**
  - **`BinColour`:** Use the `BinColour` enum for the `Colour` property of the `Bin` model.
  - **`BinType`:** Use the `BinType` enum for the `Type` property of the `Bin` model.
  - This provides type safety and avoids the use of "magic strings."

- **Collection Expressions (C# 12):** Use modern C# 12 collection expression syntax for creating readonly collections.
  - **Static Collections:** Use `[item1, item2]` syntax for inline initialization of `IReadOnlyCollection<T>` properties.
    ```c#
    Keys = [ "BIN_ID_IN_DATA" ]
    Keys = [ "ID_1", "ID_2" ]  // Multiple items
    ```
  - **Runtime Collections:** Use the spread operator `[.. collection]` to convert `List<T>` or other enumerables to readonly collections.
    ```c#
    Addresses = [.. addresses]
    ```
  - **Do NOT use:** `.AsReadOnly()` or `new List<T>() { }.AsReadOnly()` patterns. These are legacy patterns that have been replaced throughout the codebase.
  - **Benefits:** Collection expressions are more concise, performant, and represent modern C# best practices.

- **Object Initialization Patterns:**
  - **Always use multi-line initialization** for objects with 2+ properties. Never use single-line.
  - **Always use trailing commas** after every property in multi-line initializers.
  - **Always use separate variable declarations**, not inline returns:
    ```c#
    // Correct
    var clientSideRequest = new ClientSideRequest
    {
        RequestId = 1,
        Url = "https://example.com",
        Method = "GET",
    };

    var response = new GetAddressesResponse
    {
        NextClientSideRequest = clientSideRequest
    };

    return response;

    // Incorrect - no trailing commas, inline return
    return new GetAddressesResponse {
        NextClientSideRequest = new ClientSideRequest {
            RequestId = 1
        }
    };
    ```
  - Use target-typed `new()` for bin objects: `new() { Name = "...", }`
  - Use dictionary initializer for headers: `new() { {"key", "value"}, }`

- **Common Utilities:**
  - **User Agent:** Use `Constants.UserAgent` for user-agent headers, never hard-code.
  - **Form Data:** Use `ProcessingUtilities.ConvertDictionaryToFormData(new() { ... })` to convert dictionaries to URL-encoded form data.
  - **Bin Matching:** Use `ProcessingUtilities.GetMatchingBins(_binTypes, sourceKey)` to find bins by keys.
  - **Bin Day Processing:** Always call `ProcessingUtilities.ProcessBinDays(binDays)` as the final step in `GetBinDays()` to filter future dates and merge by date.
  - **Date Parsing Without Year:** When source data lacks a year (e.g., "Monday 29 December" or "15 March"), use the `ParseDateInferringYear()` extension method:
    ```c#
    using BinDays.Api.Collectors.Utilities;

    var date = dateString.ParseDateInferringYear("dddd d MMMM");
    // or
    var date = dateString.ParseDateInferringYear("d MMM");
    ```
    This method automatically infers the correct year by checking current, previous, and next years, returning the date chronologically closest to today. Useful for handling year boundaries (e.g., parsing "December 29" on January 1st).

- **Iteration Patterns:**
  - **Regex Matches:** Use `foreach (Match item in regex.Matches(content)!)` pattern.
  - **List Building:** Create mutable list, add items in loop, then convert to readonly:
    ```c#
    var addresses = new List<Address>();
    foreach (Match match in AddressRegex().Matches(content)!)
    {
        var address = new Address { ... };
        addresses.Add(address);
    }
    return [.. addresses];
    ```
  - **Comment Pattern:** Use `// Iterate through each X, and create a new X object` before foreach loops.

- **Bin Type Structure:**
  - Name the field `_binTypes` (lowercase, underscore prefix).
  - Always use `private readonly IReadOnlyCollection<Bin>` for standard collectors.
  - Always use `protected override IReadOnlyCollection<Bin> BinTypes` for vendor base collectors.
  - Required properties: `Name`, `Colour`, `Keys`.
  - Optional property: `Type` (defaults to `BinType.Bin` if omitted).
  - Use trailing commas after each property AND after each bin object in the collection.

## Code Examples

This section provides complete code templates for new collectors and their corresponding integration tests.

### Example Standard Collector Template (GovUkCollectorBase)

```c#
namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
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
	private readonly IReadOnlyCollection<Bin> _binTypes =
	[
		new()
		{
			Name = "Rubbish",
			Colour = BinColour.Grey,
			Keys = [ "RUBBISH_KEY" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Blue,
			Keys = [ "RECYCLING_KEY" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Green,
			Keys = [ "GARDEN_KEY" ],
			Type = BinType.Bag,
		},
	];

	/// <summary>
	/// Regex for the addresses from the data.
	/// </summary>
	[GeneratedRegex(@"<option\s+value=""(?<uid>[^""]+)""[^>]*>\s*(?<address>.*?)\s*</option>")]
	private static partial Regex AddressRegex();

	/// <summary>
	/// Regex for the bin days from the data.
	/// </summary>
	[GeneratedRegex(@"<tr>\s*<td>(?<date>[^<]+)</td>\s*<td>(?<service>[^<]+)</td>\s*</tr>")]
	private static partial Regex BinDaysRegex();

	/// <inheritdoc/>
	public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting addresses
		if (clientSideResponse == null)
		{
			// Prepare client-side request
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://www.mynewcouncil.gov.uk/bin-collections",
				Method = "GET",
				Headers = new() {
					{"user-agent", Constants.UserAgent},
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 1)
		{
			// Get addresses from response
			var rawAddresses = AddressRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each address, and create a new address object
			var addresses = new List<Address>();
			foreach (Match rawAddress in rawAddresses)
			{
				var uid = rawAddress.Groups["uid"].Value;

				// Exclude placeholder/invalid options
				if (uid == "-1")
				{
					continue;
				}

				var address = new Address
				{
					Property = rawAddress.Groups["address"].Value.Trim(),
					Postcode = postcode,
					Uid = uid,
				};

				addresses.Add(address);
			}

			var getAddressesResponse = new GetAddressesResponse
			{
				Addresses = [.. addresses],
			};

			return getAddressesResponse;
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}

	/// <inheritdoc/>
	public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
	{
		// Prepare client-side request for getting bin days
		if (clientSideResponse == null)
		{
			// Prepare client-side request
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://www.mynewcouncil.gov.uk/bin-collections?uprn={address.Uid}",
				Method = "GET",
				Headers = new() {
					{"user-agent", Constants.UserAgent},
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 1)
		{
			// Get bin days from response
			var rawBinDays = BinDaysRegex().Matches(clientSideResponse.Content)!;

			// Iterate through each bin day, and create a new bin day object
			var binDays = new List<BinDay>();
			foreach (Match rawBinDay in rawBinDays)
			{
				var service = rawBinDay.Groups["service"].Value.Trim();
				var collectionDate = rawBinDay.Groups["date"].Value.Trim();

				var date = DateOnly.ParseExact(
					collectionDate,
					"dd/MM/yyyy",
					CultureInfo.InvariantCulture,
					DateTimeStyles.None
				);

				// Get matching bin types from the service using the keys
				var matchedBinTypes = ProcessingUtilities.GetMatchingBins(_binTypes, service);

				var binDay = new BinDay
				{
					Date = date,
					Address = address,
					Bins = matchedBinTypes,
				};

				binDays.Add(binDay);
			}

			var getBinDaysResponse = new GetBinDaysResponse
			{
				BinDays = ProcessingUtilities.ProcessBinDays(binDays),
			};

			return getBinDaysResponse;
		}

		// Throw exception for invalid request
		throw new InvalidOperationException("Invalid client-side request.");
	}
}
```

### Example Vendor Base Collector Template (ITouchVisionCollectorBase)

For collectors using shared vendor platforms (like ITouchVision, FCC, or Binzone), inherit from the appropriate vendor base class. These implementations are much simpler:

```c#
namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using System;
using System.Collections.Generic;

/// <summary>
/// Collector implementation for My Vendor Council.
/// </summary>
internal sealed class MyVendorCouncil : ITouchVisionCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "My Vendor Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.myvendorcouncil.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "my-vendor-council";

	/// <inheritdoc/>
	protected override int ClientId => 123;

	/// <inheritdoc/>
	protected override int CouncilId => 45678;

	/// <inheritdoc/>
	protected override string ApiBaseUrl => "https://iweb.itouchvision.com/portal/itouchvision/";

	/// <inheritdoc/>
	protected override IReadOnlyCollection<Bin> BinTypes =>
	[
		new()
		{
			Name = "Rubbish",
			Colour = BinColour.Black,
			Keys = [ "Rubbish" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden Waste" ],
		},
	];
}
```

**Note:**
- No `GetAddresses()` or `GetBinDays()` methods—handled by the base class.
- No regex patterns required.
- Uses `protected override` for all inherited properties.
- `BinTypes` is a property, not a field.
- Class is `sealed` but **not** `partial` (no regex methods).

### Example Integration Test Template

```c#
namespace BinDays.Api.IntegrationTests.Collectors.Councils;

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
```

## Testing and Committing

After implementing your collector and its integration test, follow these final steps.

### Integration Tests

- **Requirement:** Every new collector **must** have an accompanying integration test.
- **Test Helper:** Use the `TestSteps.EndToEnd` helper for end-to-end testing of the collector.
- **Test Naming:** Test methods should be named descriptively (e.g. `GetBinDaysTest`).

### Commit Messages

- **Be Descriptive:** Write clear and descriptive commit messages that explain the "what" and "why" of the changes.
- **Reference Issues:** If your commit addresses an issue, reference it in the commit message (e.g. `Fixes #123`).