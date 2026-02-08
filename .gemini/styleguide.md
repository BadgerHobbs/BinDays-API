# BinDays-API Collector Style Guide

This comprehensive guide outlines all coding conventions, design principles, and best practices for implementing collectors in the BinDays-API project. It combines structural requirements with detailed do's and don'ts examples based on code reviews of all open pull requests.

## Table of Contents

### Part 1: Structure & Requirements

1. [Key Principles](#key-principles)
2. [File Structure](#file-structure)
3. [Naming Conventions](#naming-conventions)
4. [Property Implementation](#property-implementation)
5. [Class Declaration](#class-declaration)
6. [Class Member Ordering](#class-member-ordering)
7. [Core Design Philosophy](#core-design-philosophy)

### Part 2: Implementation Guidelines (Do's and Don'ts)

8. [HTTP Headers](#http-headers)
9. [Request Bodies & JSON Payloads](#request-bodies--json-payloads)
10. [Bin Type Configuration](#bin-type-configuration)
11. [Postcode Handling](#postcode-handling)
12. [Metadata Management](#metadata-management)
13. [Null Handling & Required Values](#null-handling--required-values)
14. [Code Formatting & Style](#code-formatting--style)
15. [Helper Methods & Code Duplication](#helper-methods--code-duplication)
16. [Collector Complexity](#collector-complexity)
17. [Constants & Configuration](#constants--configuration)
18. [Date Parsing](#date-parsing)
19. [Regular Expressions](#regular-expressions)
20. [Address Handling](#address-handling)
21. [GetBinDays Patterns](#getbindays-patterns)
22. [Iteration Patterns](#iteration-patterns)
23. [URL Encoding](#url-encoding)

### Part 3: Templates & Testing

24. [Code Examples & Templates](#code-examples--templates)
25. [Testing Requirements](#testing-requirements)
26. [Quick Reference Checklist](#quick-reference-checklist)

---

# Part 1: Structure & Requirements

## Key Principles

### Readability, Maintainability, Consistency

- Code should be easy to understand for all team members
- Code should be easy to modify and extend
- Adhering to a consistent style across all projects improves collaboration and reduces errors

### No Browser Emulation

Do not use browser emulation tools like Selenium. Instead, replicate the necessary HTTP requests directly. This keeps collectors lightweight and avoids heavy dependencies.

### Return Only Actual Data

Collectors must return ONLY the collection dates explicitly provided by the council website. Never calculate or infer additional dates based on intervals, patterns, or statements like "and every other week thereafter". The collector's responsibility is to faithfully represent the data source, not to project or compute future dates.

### Keep Collectors Simple

Most collectors should be 200-400 lines. If approaching 500+ lines, it's a code smell indicating unnecessary complexity, bloated payloads, or single-use helpers.

---

## File Structure

- **New Collectors**: Place new council collector classes in `BinDays.Api.Collectors/Collectors/Councils/`
  - Filename must match the class name (e.g. `MyNewCouncil.cs`)

- **Integration Tests**: Corresponding integration tests for new collectors must be placed in `BinDays.Api.IntegrationTests/Collectors/Councils/`
  - Test filename should be `[CollectorName]Tests.cs` (e.g. `MyNewCouncilTests.cs`)

---

## Naming Conventions

- **Collector Classes**: Use PascalCase (e.g. `MyNewCouncil`)
- **Interfaces**: Use the `I` prefix (e.g. `ICollector`)
- **Methods and Properties**: Use PascalCase (e.g. `GetAddresses`, `WebsiteUrl`)
- **Private Fields**: Use `_camelCase` (e.g. `_binTypes`, `_client`)
- **Regex Methods**: Use PascalCase with "Regex" suffix (e.g. `TokenRegex()`, `AddressRegex()`, `BinDaysRegex()`)

---

## Property Implementation

- **Expression-Bodied Members**: Always use expression-bodied syntax (`=>`) for property getters, never full property blocks
- **Documentation**: Always use `/// <inheritdoc/>` for interface properties (`Name`, `WebsiteUrl`, `GovUkId`)
- **Uri Instantiation**: Use target-typed `new("url")` syntax for Uri properties

Example:

```c#
/// <inheritdoc/>
public string Name => "My New Council";

/// <inheritdoc/>
public Uri WebsiteUrl => new("https://www.mynewcouncil.gov.uk/");

/// <inheritdoc/>
public override string GovUkId => "my-new-council";
```

---

## Class Declaration

- **Access Modifier**: Always use `internal` for collector classes (not `public`)
- **Sealed**: Always declare collectors as `sealed` to prevent inheritance
- **Partial**: Use `partial` **only** when the class uses `[GeneratedRegex]` attributes
  - Standard collectors with regex: `internal sealed partial class`
  - Vendor base collectors without regex: `internal sealed class`
- **Inheritance**: All collectors inherit from `GovUkCollectorBase` (directly or through a vendor base class) and explicitly implement `ICollector`

Examples:

```c#
// Standard collector with regex - requires partial
internal sealed partial class MyNewCouncil : GovUkCollectorBase, ICollector

// Vendor base collector without regex - no partial needed
internal sealed class MyVendorCouncil : ITouchVisionCollectorBase, ICollector
```

---

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
   - **Only keep constants used 2+ times**: If referenced only once, inline it directly
   - **Never use constants for bin names**: Bin names should be inline strings in `_binTypes`
   - Place all const fields together after bin types, before regex methods

### Implementation (in this order):

4. **Regex Methods** (if using `[GeneratedRegex]`):
   - Typical order: `TokenRegex()`, `AddressRegex()`, `BinDaysRegex()`
   - Other helper regexes as needed

5. **Interface Methods**:
   - `GetAddresses()`
   - `GetBinDays()`

6. **Helper Methods** (if needed):
   - Place at the end after interface methods
   - Always include XML documentation (`/// <summary>`)

### Example structure:

```c#
internal sealed partial class MyCouncil : GovUkCollectorBase, ICollector
{
    // === CONFIGURATION ===

    // Interface properties
    public string Name => "...";
    public Uri WebsiteUrl => new("...");
    public override string GovUkId => "...";

    // Bin types
    private readonly IReadOnlyCollection<Bin> _binTypes = [...];

    // Private const fields (if needed, and only if used 2+ times)
    private const string _apiKey = "...";

    // === IMPLEMENTATION ===

    // Regex methods
    [GeneratedRegex(@"...")]
    private static partial Regex AddressRegex();

    // Interface methods
    public GetAddressesResponse GetAddresses(...) { ... }
    public GetBinDaysResponse GetBinDays(...) { ... }

    // Helper methods (if needed, and only if used 2-3+ times)
    /// <summary>
    /// Parses bin collection data from the API response.
    /// </summary>
    private List<BinDay> ParseBinData(...) { ... }
}
```

---

## Core Design Philosophy

### Stateless Design

Because all requests to council websites originate from the client application (the user's device), the API itself is stateless. This means you cannot save state, such as authentication tokens or session cookies, between the different steps of a collector's process (e.g. between `GetAddresses` and `GetBinDays`). Each step is an independent transaction.

### Managing State Across Request Steps

While the collector itself is stateless, you often need to pass data (tokens, cookies, session IDs) between client-side request steps. Use the `ClientSideOptions.Metadata` dictionary to carry this state forward:

```c#
// Store state in first request
Options = new ClientSideOptions
{
    Metadata =
    {
        { "cookie", cookie },
        { "csrfToken", token },
    },
};

// Retrieve state in subsequent request
var cookie = clientSideResponse.Options.Metadata["cookie"];
var token = clientSideResponse.Options.Metadata["csrfToken"];
```

**Important guidelines for metadata:**

- **Only store necessary data**: Don't add metadata keys that aren't used in subsequent requests
- **Expect required values**: If metadata is needed for the next step, expect it to exist and use the null-forgiving operator (`!`)
- **Avoid unnecessary keys**: Don't store values that can be easily derived or aren't needed

### Step-by-Step Request Implementation

For collectors that require multiple client-side requests, use a step-by-step implementation based on the `RequestId` of the `clientSideResponse`. This is typically structured as an `if/else if` chain.

- **RequestId Numbering**: Always start RequestId at `1` (not `0`) and increment sequentially
- **Initial Request Pattern**: Always start with `if (clientSideResponse == null)` for the initial client-side request
- **Subsequent Requests**: Use `else if (clientSideResponse.RequestId == X)` for each step in the flow
- **Fallthrough Handler**: Always end with `throw new InvalidOperationException("Invalid client-side request.");`
- **Comment Style**: Use clear, specific comments before each block
- **Preserve Existing Logic**: Do not refactor the existing `if/else if` structure—this pattern is intentional

### Intentionally Brittle Design

Collectors are intentionally designed to be "brittle"—they are expected to fail loudly and quickly if the council's website changes or if the data format is not what is expected.

- **Avoid `try/catch` blocks**: Do not wrap parsing logic in `try/catch` blocks to handle nulls or formatting issues silently
- **Let the code raise exceptions**: `NullReferenceException`, `FormatException`, etc., should propagate
- **Why?** This ensures errors are not hidden. When a collector fails, the error is captured and logged at a higher level with a clear stack trace
- **Custom Exceptions**: For predictable, high-level failures (e.g. postcode not found), create and throw a custom exception (e.g. `GovUkIdNotFoundException`)

---

# Part 2: Implementation Guidelines (Do's and Don'ts)

## HTTP Headers

### ❌ DON'T: Include excessive browser-specific headers

**Problem**: Too many browser-specific headers make the collector brittle and heavyweight.

```c#
Headers = new()
{
    { "user-agent", Constants.UserAgent },
    { "accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8" },
    { "accept-language", "en-US,en;q=0.9" },
    { "sec-ch-ua", "\"Not A(Brand\";v=\"99\", \"Mozilla\";v=\"137\", \"Chromium\";v=\"137\"" },
    { "sec-ch-ua-mobile", "?0" },
    { "upgrade-insecure-requests", "1" },
},
```

### ✅ DO: Use minimal, essential headers only

**Reason**: Start with the minimum required headers. Only add additional headers if the API strictly requires them.

```c#
Headers = new()
{
    { "user-agent", Constants.UserAgent },
},
```

**Or if content type is needed:**

```c#
Headers = new()
{
    { "user-agent", Constants.UserAgent },
    { "content-type", "application/json" },
},
```

### ❌ DON'T: Use old-style Dictionary initialization

**Problem**: Using `new Dictionary<string, string>()` instead of target-typed `new()`.

```c#
var requestHeaders = new Dictionary<string, string> {
    { "user-agent", Constants.UserAgent },
};
```

### ✅ DO: Use target-typed new() for dictionaries

**Reason**: More concise and consistent with modern C# style.

```c#
Dictionary<string, string> requestHeaders = new()
{
    { "user-agent", Constants.UserAgent },
    { "content-type", "application/x-www-form-urlencoded" },
};
```

---

## Request Bodies & JSON Payloads

### ❌ DON'T: Include unnecessary fields in request bodies

**Problem**: Bloated payloads with empty strings, null values, default values, or false booleans.

```c#
Body = $$"""
{
    "postcode": "{{postcode}}",
    "uprn": "{{uprn}}",
    "timestamp": "{{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}",
    "source": "",
    "validate": false,
    "returnAll": null,
    "locale": ""
}
""",
```

### ✅ DO: Use minimal request bodies

**Reason**: Remove all unnecessary fields. Only include what the API actually requires.

```c#
Body = $$"""
{
    "postcode": "{{postcode}}",
    "uprn": "{{uprn}}"
}
""",
```

### ❌ DON'T: Build JSON using nested dictionaries

**Problem**: Nested dictionaries are verbose and hard to read.

```c#
var payload = JsonSerializer.Serialize(new Dictionary<string, object>
{
    { "formId", formId },
    { "sessionId", sessionId },
    { "formValues", new Dictionary<string, string>
        {
            { "postcode", postcode },
            { "uprn", uprn }
        }
    }
});
```

### ✅ DO: Use raw string literals for JSON

**Reason**: Raw string literals with interpolation are more readable and maintainable.

```c#
var payload = $$"""
{
    "formId": "{{formId}}",
    "sessionId": "{{sessionId}}",
    "formValues": {
        "postcode": "{{postcode}}",
        "uprn": "{{uprn}}"
    }
}
""";
```

---

## Bin Type Configuration

### ❌ DON'T: Use generic bin names

**Problem**: Names that only describe the container type/color, not the contents.

```c#
private readonly IReadOnlyCollection<Bin> _binTypes =
[
    new()
    {
        Name = "Recycling (Green Box)",
        Colour = BinColour.Green,
        Keys = [ "Recycling" ],
        Type = BinType.Box,
    },
];
```

### ✅ DO: Use descriptive bin names

**Reason**: Names should describe what goes in the bin, not just the container.

```c#
private readonly IReadOnlyCollection<Bin> _binTypes =
[
    new()
    {
        Name = "Paper, Glass & Cardboard Recycling",
        Colour = BinColour.Green,
        Keys = [ "Recycling" ],
        Type = BinType.Box,
    },
];
```

### ❌ DON'T: Include unnecessary keys

**Problem**: Multiple keys when only one is actually needed for matching.

```c#
Keys = [ "Residual", "Refuse", "General" ],
```

### ✅ DO: Include only keys that are actually matched

**Reason**: Only add keys that will actually be matched against the data source.

```c#
Keys = [ "General Waste" ],
```

### ❌ DON'T: Extract bin names as constants

**Problem**: Creating constants for bin names adds unnecessary code.

```c#
private const string _rubbishBinName = "General Waste";

private readonly IReadOnlyCollection<Bin> _binTypes =
[
    new()
    {
        Name = _rubbishBinName,  // ❌
        Colour = BinColour.Black,
    },
];
```

### ✅ DO: Inline bin names

**Reason**: Bin names should be inline strings in the `_binTypes` collection.

```c#
private readonly IReadOnlyCollection<Bin> _binTypes =
[
    new()
    {
        Name = "General Waste",  // ✅
        Colour = BinColour.Black,
    },
];
```

### ❌ DON'T: Explicitly set default Type value

**Problem**: Setting `Type = BinType.Bin` when it's the default.

```c#
new()
{
    Name = "General Waste",
    Colour = BinColour.Black,
    Type = BinType.Bin,  // ❌ This is the default
},
```

### ✅ DO: Omit default Type value

**Reason**: `BinType.Bin` is the default value, so it can be omitted.

```c#
new()
{
    Name = "General Waste",
    Colour = BinColour.Black,
    // No Type property needed - defaults to BinType.Bin
},
```

---

## Postcode Handling

### ❌ DON'T: Format the postcode in collectors

**Problem**: Calling `FormatPostcode()` inside the collector implementation.

```c#
public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
{
    if (clientSideResponse == null)
    {
        postcode = ProcessingUtilities.FormatPostcode(postcode);  // ❌ Don't do this
        // ...
    }
}
```

### ✅ DO: Use the postcode as-is

**Reason**: The postcode is already formatted before the collector method is called. Use it directly.

```c#
public GetAddressesResponse GetAddresses(string postcode, ClientSideResponse? clientSideResponse)
{
    if (clientSideResponse == null)
    {
        // Use postcode directly - it's already formatted
        var url = $"https://api.example.com?postcode={postcode}";
        // ...
    }
}
```

### ❌ DON'T: Extract postcode from address data

**Problem**: Parsing the postcode from raw address strings when it's already provided.

```c#
var fullAddress = addressElement.GetProperty("fullAddress").GetString()!;
var extractedPostcode = Regex.Match(fullAddress, @"[A-Z]{1,2}\d{1,2}\s?\d[A-Z]{2}").Value;  // ❌

var address = new Address
{
    Property = fullAddress,
    Postcode = extractedPostcode,  // ❌ Don't extract it
    Uid = uid,
};
```

### ✅ DO: Use the postcode parameter

**Reason**: The postcode is passed as a parameter - use it directly.

```c#
var fullAddress = addressElement.GetProperty("fullAddress").GetString()!;

var address = new Address
{
    Property = fullAddress,
    Postcode = postcode,  // ✅ Use the parameter
    Uid = uid,
};
```

---

## Metadata Management

### ❌ DON'T: Store unnecessary metadata

**Problem**: Storing values in metadata that aren't used in subsequent requests.

```c#
Options = new ClientSideOptions
{
    Metadata =
    {
        { "postcode", formattedPostcode },  // Not used later
        { "requestTime", DateTime.UtcNow.ToString() },  // Not used later
        { "originalInput", postcode },  // Not used later
    },
},
```

### ✅ DO: Only store metadata that's needed later

**Reason**: Metadata should only contain values required for subsequent requests.

```c#
Options = new ClientSideOptions
{
    Metadata =
    {
        { "csrfToken", token },  // ✅ Used in next request
        { "sessionId", sessionId },  // ✅ Used in next request
    },
},
```

### ❌ DON'T: Initialize empty metadata explicitly

**Problem**: Creating empty metadata dictionaries unnecessarily.

```c#
Options = new ClientSideOptions
{
    Metadata = new Dictionary<string, string>(),  // ❌ Unnecessary
},
```

### ✅ DO: Omit Options/Metadata if not needed

**Reason**: Properties with default values should be omitted entirely.

```c#
var clientSideRequest = new ClientSideRequest
{
    RequestId = 1,
    Url = "https://example.com",
    Method = "GET",
    // No Options property at all if no metadata needed
};
```

### ❌ DON'T: Make required metadata optional

**Problem**: Using conditional logic when metadata should always exist.

```c#
var cookie = clientSideResponse.Options.Metadata.ContainsKey("cookie")
    ? clientSideResponse.Options.Metadata["cookie"]
    : string.Empty;
```

### ✅ DO: Expect required metadata to exist

**Reason**: If metadata is needed for the next step, it should always be there. Fail fast if it's missing.

```c#
var cookie = clientSideResponse.Options.Metadata["cookie"];
```

---

## Null Handling & Required Values

### ❌ DON'T: Use null-coalescing for required values

**Problem**: Silently using fallback values when data should be required.

```c#
var postcode = address.Postcode ?? string.Empty;
var uid = address.Uid ?? "unknown";

var cookies = clientSideResponse.Headers.TryGetValue("set-cookie", out var h)
    ? ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(h)
    : string.Empty;
```

### ✅ DO: Use null-forgiving operator for required values

**Reason**: Let the collector fail fast with clear errors if required data is missing.

```c#
var postcode = address.Postcode!;
var uid = address.Uid!;

var setCookieHeader = clientSideResponse.Headers["set-cookie"];
var cookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);
```

### ❌ DON'T: Use TryGetValue for required headers

**Problem**: TryGetValue with null-forgiving operator is unnecessarily verbose for headers that must exist.

```c#
clientSideResponse.Headers.TryGetValue("set-cookie", out var setCookieHeader);
var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader!);
```

### ✅ DO: Use direct header indexer access

**Reason**: Simpler and fails fast with a clear `KeyNotFoundException` if the header is missing.

```c#
var setCookieHeader = clientSideResponse.Headers["set-cookie"];
var requestCookies = ProcessingUtilities.ParseSetCookieHeaderForRequestCookie(setCookieHeader);
```

### ❌ DON'T: Use defensive fallback for expected JSON properties

**Problem**: Making optional what should be required.

```c#
if (data.TryGetProperty("uprn", out var uprnElement))
{
    uid = uprnElement.GetString() ?? "fallback";
}
else
{
    uid = data.GetProperty("name").GetString();  // Fallback
}
```

### ✅ DO: Expect required properties and fail fast

**Reason**: If data should always exist, don't handle the missing case.

```c#
var uid = data.GetProperty("uprn").GetString()!;
```

---

## Code Formatting & Style

### ❌ DON'T: Forget trailing commas

**Problem**: Missing trailing commas in multi-line initializers.

```c#
private readonly IReadOnlyCollection<Bin> _binTypes =
[
    new()
    {
        Name = "General Waste",
        Colour = BinColour.Black,
        Keys = [ "General" ]  // ❌ Missing comma
    }
];

Headers = new()
{
    { "user-agent", Constants.UserAgent }  // ❌ Missing comma
};
```

### ✅ DO: Always use trailing commas

**Reason**: Makes future diffs cleaner and reduces merge conflicts.

```c#
private readonly IReadOnlyCollection<Bin> _binTypes =
[
    new()
    {
        Name = "General Waste",
        Colour = BinColour.Black,
        Keys = [ "General" ],  // ✅ Has comma
    },
];

Headers = new()
{
    { "user-agent", Constants.UserAgent },  // ✅ Has comma
};
```

### ❌ DON'T: Use single-line date parsing

**Problem**: Date parsing on a single line is hard to read.

```c#
var date = DateOnly.ParseExact(dateString, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None);
```

### ✅ DO: Use multi-line date parsing

**Reason**: Multi-line format improves readability.

```c#
var date = DateOnly.ParseExact(
    dateString,
    "dd/MM/yyyy",
    CultureInfo.InvariantCulture,
    DateTimeStyles.None
);
```

### ❌ DON'T: Put closing parenthesis inline

**Problem**: Closing parenthesis on the same line as last argument.

```c#
var date = DateOnly.ParseExact(
    dateString,
    "dd/MM/yyyy",
    CultureInfo.InvariantCulture,
    DateTimeStyles.None);  // ❌ Should be on own line
```

### ✅ DO: Put closing parenthesis on separate line

**Reason**: Consistent formatting for multi-line method calls.

```c#
var date = DateOnly.ParseExact(
    dateString,
    "dd/MM/yyyy",
    CultureInfo.InvariantCulture,
    DateTimeStyles.None
);  // ✅ On its own line
```

### ❌ DON'T: Include unused using statements

**Problem**: Importing namespaces that aren't used in the file.

```c#
using BinDays.Api.Collectors.Collectors.Vendors;  // Not used
using System.Linq;  // Not used
using System.Web;  // Legacy, should use System.Net
```

### ✅ DO: Remove unused using statements

**Reason**: Keeps code clean and reduces clutter.

```c#
// Only include what you actually use
using BinDays.Api.Collectors.Models;
using BinDays.Api.Collectors.Utilities;
using System.Net;  // Use modern WebUtility, not System.Web.HttpUtility
```

### ❌ DON'T: Return inline without separate variable

**Problem**: Returning objects inline makes debugging harder.

```c#
return new GetAddressesResponse {
    NextClientSideRequest = new ClientSideRequest {
        RequestId = 1
    }
};
```

### ✅ DO: Use separate variable declarations

**Reason**: Improves readability and makes debugging easier.

```c#
var clientSideRequest = new ClientSideRequest
{
    RequestId = 1,
    Url = "https://example.com",
};

var response = new GetAddressesResponse
{
    NextClientSideRequest = clientSideRequest
};

return response;
```

### ❌ DON'T: Use partial keyword without GeneratedRegex

**Problem**: Declaring class as partial when it doesn't use regex.

```c#
internal sealed partial class MyCouncil : GovUkCollectorBase, ICollector
{
    // No [GeneratedRegex] attributes anywhere
}
```

### ✅ DO: Only use partial for classes with GeneratedRegex

**Reason**: The partial keyword is only needed when using `[GeneratedRegex]` attributes.

```c#
// Without regex - no partial needed
internal sealed class MyCouncil : GovUkCollectorBase, ICollector

// With regex - partial required
internal sealed partial class MyCouncil : GovUkCollectorBase, ICollector
{
    [GeneratedRegex(@"<option value=""(?<uid>[^""]+)"">")]
    private static partial Regex AddressRegex();
}
```

---

## Helper Methods & Code Duplication

### ❌ DON'T: Create single-use helper methods

**Problem**: Extracting methods that are only called once for "organization".

```c#
private static Dictionary<string, object> BuildAddressSearchBody(string postcode)
{
    var body = new Dictionary<string, object>
    {
        { "postcode", postcode },
        { "search", true }
    };
    return body;
}

// Called only once
var requestBody = JsonSerializer.Serialize(BuildAddressSearchBody(postcode));
```

### ✅ DO: Inline single-use code

**Reason**: Single-use helpers make the code harder to follow, not easier.

```c#
// Inline where it's used
var requestBody = $$"""
{
    "postcode": "{{postcode}}",
    "search": true
}
""";
```

### ❌ DON'T: Create helpers without documentation

**Problem**: Helper methods missing XML documentation.

```c#
private static string ProcessDateString(string input)
{
    return input.Trim().Replace("st", "").Replace("nd", "");
}
```

### ✅ DO: Add XML documentation to helpers

**Reason**: All helper methods require XML documentation.

```c#
/// <summary>
/// Removes ordinal suffixes from date strings.
/// </summary>
private static string ProcessDateString(string input)
{
    return input.Trim().Replace("st", "").Replace("nd", "");
}
```

### ✅ DO: Extract truly duplicated logic (2-3+ uses)

**Reason**: Helpers should reduce duplication when code is used multiple times.

```c#
/// <summary>
/// Creates the initial client-side request for session initialization.
/// </summary>
private ClientSideRequest CreateInitialRequest()
{
    return new ClientSideRequest
    {
        RequestId = 1,
        Url = "https://example.com/form",
        Method = "GET",
        Headers = new() { { "user-agent", Constants.UserAgent }, },
    };
}

// Used in both GetAddresses and GetBinDays
var clientSideRequest = CreateInitialRequest();
```

---

## Collector Complexity

### ❌ DON'T: Create overly complex collectors

**Problem**: Collector implementations that are 500+ lines long indicate a code smell.

**Red flags that indicate unnecessary complexity:**

- Single-use helper methods
- Excessive comments explaining convoluted logic
- Multiple retry mechanisms
- Complex state tracking across requests
- Defensive null checks and fallback logic
- Building JSON with nested Dictionary structures
- Duplicate code between GetAddresses and GetBinDays

### ✅ DO: Keep collectors simple and focused

**Reason**: Most collectors should be 200-400 lines. If it's much longer, look for unnecessary complexity.

**How to reduce complexity:**

1. **Remove bloated request bodies** - Strip unnecessary fields
2. **Inline single-use helpers** - Don't extract methods used once
3. **Use raw string literals** - Replace nested Dictionary structures
4. **Avoid over-engineering** - Don't add retry logic, caching, or defensive fallbacks unless strictly required
5. **Minimize metadata** - Only store what's needed for subsequent requests
6. **Extract truly common logic** - Only create helpers for genuine duplication (2-3+ uses)

---

## Constants & Configuration

### ❌ DON'T: Create constants for single-use values

**Problem**: Creating constants that are only referenced once.

```c#
private const string _pageUrl = "https://www.example.com/bins";

// Used only once
var clientSideRequest = new ClientSideRequest
{
    Url = _pageUrl,
};
```

### ✅ DO: Inline values used only once

**Reason**: Constants should only exist for values used 2+ times.

```c#
var clientSideRequest = new ClientSideRequest
{
    Url = "https://www.example.com/bins",
};
```

### ✅ DO: Create constants for repeated values

**Reason**: DRY principle applies when a value is used multiple times.

```c#
private const string _apiKey = "abc123xyz";

// Used in multiple requests
Headers = new() { { "x-api-key", _apiKey }, };
// ... later
Headers = new() { { "x-api-key", _apiKey }, };
```

### ✅ DO: Use HTTPS URLs when available

**Reason**: More secure and avoids unnecessary redirects.

```c#
// ❌ DON'T
public Uri WebsiteUrl => new("http://www.example.gov.uk/");

// ✅ DO
public Uri WebsiteUrl => new("https://www.example.gov.uk/");
```

---

## Date Parsing

### ❌ DON'T: Use fragile string manipulation

**Problem**: Using `Split` or other string operations that can break easily.

```c#
var day = dateText.Split(' ')[0];  // Breaks if format changes
```

### ✅ DO: Use regex for extracting date parts

**Reason**: More robust when format varies slightly.

```c#
var day = Regex.Match(dateText, @"\d+").Value;
```

### ❌ DON'T: Forget to remove ordinal suffixes

**Problem**: Parsing dates with "1st", "2nd", "3rd" without removing suffixes.

```c#
var date = DateOnly.ParseExact(
    "21st March 2024",  // ❌ Will fail
    "dd MMMM yyyy",
    CultureInfo.InvariantCulture
);
```

### ✅ DO: Remove ordinal suffixes before parsing

**Reason**: UK councils frequently use ordinal date formats.

```c#
[GeneratedRegex(@"(?<=\d)(st|nd|rd|th)")]
private static partial Regex OrdinalSuffixRegex();

// Remove suffixes first
dateString = OrdinalSuffixRegex().Replace(dateString, "");
var date = DateOnly.ParseExact(
    dateString,
    "d MMMM yyyy",
    CultureInfo.InvariantCulture,
    DateTimeStyles.None
);
```

### ✅ DO: Use ParseDateInferringYear for dates without year

**Reason**: Extension method automatically handles year boundaries.

```c#
using BinDays.Api.Collectors.Utilities;

// When date lacks year information
var date = dateString.ParseDateInferringYear("dddd d MMMM");
```

---

## Regular Expressions

### ❌ DON'T: Access regex groups by index

**Problem**: Using numeric indexes instead of named capture groups.

```c#
[GeneratedRegex(@"sid=([^&]+)")]
private static partial Regex SidRegex();

// Later...
var sid = SidRegex().Match(content).Groups[1].Value;  // ❌ Using index
```

### ✅ DO: Use named capture groups

**Reason**: More readable and maintainable. Safer if regex changes.

```c#
[GeneratedRegex(@"sid=(?<sid>[^&]+)")]
private static partial Regex SidRegex();

// Later...
var sid = SidRegex().Match(content).Groups["sid"].Value;  // ✅ Using name
```

### ❌ DON'T: Forget null-forgiving operator on Matches

**Problem**: Not using `!` on regex `Matches()` calls.

```c#
var rawAddresses = AddressRegex().Matches(content);  // ❌ Missing !
```

### ✅ DO: Use null-forgiving operator on Matches

**Reason**: Ensures failures propagate clearly per the "fail fast" philosophy.

```c#
var rawAddresses = AddressRegex().Matches(content)!;  // ✅ Has !
```

### ❌ DON'T: Leave unused regex methods

**Problem**: Defining `[GeneratedRegex]` methods that aren't used.

```c#
[GeneratedRegex(@"month:\s*(?<month>\w+)")]
private static partial Regex MonthSectionRegex();  // Never used!
```

### ✅ DO: Remove unused regex methods

**Reason**: Keeps code clean and avoids confusion.

```c#
// Only define regex methods that are actually used in the code
[GeneratedRegex(@"<option value=""(?<uid>[^""]+)"">")]
private static partial Regex AddressRegex();  // ✅ Actually used
```

### ❌ DON'T: Use dynamic regex patterns

**Problem**: Creating regex patterns at runtime or using Regex.Replace/Match with string patterns.

```c#
// Building pattern at runtime
var pattern = $@"<div class=""{className}"">(.*?)</div>";
var match = Regex.Match(content, pattern);  // ❌ Not using GeneratedRegex

// Or using Regex.Replace directly
var cleaned = Regex.Replace(dateString, @"\d+(st|nd|rd|th)", "");  // ❌ Not using GeneratedRegex
```

### ✅ DO: Use GeneratedRegex for all patterns

**Reason**: Better performance and compile-time validation.

```c#
// Define all patterns as GeneratedRegex
[GeneratedRegex(@"<div class=""[^""]*"">(.*?)</div>")]
private static partial Regex ContentRegex();

[GeneratedRegex(@"\d+(st|nd|rd|th)")]
private static partial Regex OrdinalRegex();

// Use the generated methods
var match = ContentRegex().Match(content);  // ✅
var cleaned = OrdinalRegex().Replace(dateString, "");  // ✅
```

---

## Address Handling

### ❌ DON'T: Use complex if statements for address building

**Problem**: Multiple if statements to conditionally build address parts.

```c#
var property = addressParts[0];
if (addressParts.Length > 1)
{
    property += ", " + addressParts[1];
}
if (addressParts.Length > 2)
{
    property += ", " + addressParts[2];
}
```

### ✅ DO: Use string.Join with Where filter

**Reason**: More concise and handles empty/whitespace values automatically.

```c#
var property = string.Join(", ", addressParts.Where(p => !string.IsNullOrWhiteSpace(p)));
```

### ❌ DON'T: Use nested ternary operators

**Problem**: Hard to read and understand.

```c#
Town = addressParts.Length > 3
    ? addressParts[2]
    : addressParts.Length > 2
        ? addressParts[1]
        : null,
```

### ✅ DO: Use switch expressions

**Reason**: More readable and maintainable.

```c#
Town = addressParts.Length switch
{
    > 3 => addressParts[2],
    > 2 => addressParts[1],
    _ => null,
},
```

### ❌ DON'T: Split addresses unnecessarily

**Problem**: Extracting Street and Town when not required by the API.

```c#
var address = new Address
{
    Property = addressParts[0],
    Street = addressParts.Length > 1 ? addressParts[1] : null,
    Town = addressParts.Length > 2 ? addressParts[2] : null,
    Postcode = postcode,
    Uid = uid,
};
```

### ✅ DO: Use Property field for full address

**Reason**: Simpler and more accurate unless API requires separate fields.

```c#
var address = new Address
{
    Property = fullAddressString,  // Include everything here
    Postcode = postcode,
    Uid = uid,
    // Omit Street and Town unless API strictly requires them
};
```

### ❌ DON'T: Sort addresses

**Problem**: Unnecessarily sorting addresses by property name.

```c#
Addresses = addresses.OrderBy(address => address.Property).ToList(),
```

### ✅ DO: Return addresses in order received

**Reason**: Addresses should be returned in the order from the API.

```c#
Addresses = [.. addresses],
```

### ✅ DO: Concatenate multiple data parts into UID when needed

**Reason**: When `GetBinDays` requires multiple pieces of data (UPRN, property name, coordinates, etc.), concatenate them into the `Uid` field using a semicolon separator. This allows you to pass all necessary data forward without re-fetching addresses in both `GetAddresses` and `GetBinDays`.

**This pattern is far preferable to re-fetching the same address data in both methods.**

**Pattern:**

1. In `GetAddresses`: Concatenate all required data into the UID
2. In `GetBinDays`: Split the UID to extract the individual parts

**Example (WakefieldCouncil):**

```c#
// In GetAddresses - concatenate uprn and property
var address = new Address
{
    Property = property,
    Postcode = postcode,
    Uid = $"{uprn};{property}",  // Store multiple values
};
```

```c#
// In GetBinDays - split and use the parts
// Uid format: "uprn;property"
var parts = address.Uid!.Split(';', 2);
var uprn = parts[0];
var property = parts[1];

var clientSideRequest = new ClientSideRequest
{
    Url = $"https://example.com/bins?uprn={uprn}&a={Uri.EscapeDataString(property)}",
    // ...
};
```

**Important notes:**
- Use semicolon (`;`) as the separator to avoid conflicts with common address characters (commas, spaces)
- Use `Split(';', 2)` when you only need to split into a specific number of parts
- Always use the null-forgiving operator when splitting: `address.Uid!.Split(';')`
- Document the UID format with a comment in both methods for clarity

---

## GetBinDays Patterns

### ❌ DON'T: Re-fetch address data in GetBinDays

**Problem**: Repeating the address lookup logic when you already have the address data.

```c#
public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
{
    if (clientSideResponse == null)
    {
        // ❌ Re-fetching address data using address.Postcode and address.Uid
        var requestBody = $$"""
        {
            "postcode": "{{address.Postcode}}",
            "action": "searchAddress"
        }
        """;

        var clientSideRequest = new ClientSideRequest
        {
            RequestId = 1,
            Url = "https://api.example.com/addressLookup",  // ❌ Same as GetAddresses
            Method = "POST",
            Body = requestBody,
        };
    }
}
```

### ✅ DO: Use the address data you already have

**Reason**: The `Address` object already contains all the data you need (Uid, Property, Postcode). Use it directly.

```c#
public GetBinDaysResponse GetBinDays(Address address, ClientSideResponse? clientSideResponse)
{
    if (clientSideResponse == null)
    {
        // ✅ Use address.Uid directly for bin days lookup
        var requestBody = $$"""
        {
            "uprn": "{{address.Uid}}",
            "action": "getBinDays"
        }
        """;

        var clientSideRequest = new ClientSideRequest
        {
            RequestId = 1,
            Url = "https://api.example.com/binDays",  // ✅ Different endpoint
            Method = "POST",
            Body = requestBody,
        };
    }
}
```

**When is re-fetching acceptable?**

Only if the API strictly requires it (e.g. session initialization for both endpoints). Even then, consider if the session can be reused from GetAddresses.

---

## Iteration Patterns

### ❌ DON'T: Forget iteration comments

**Problem**: Missing the standard comment before foreach loops.

```c#
var addresses = new List<Address>();
foreach (Match match in rawAddresses)  // ❌ No comment
{
    // ...
}
```

### ✅ DO: Add standard iteration comments

**Reason**: Improves readability and follows project conventions.

```c#
// Iterate through each address, and create a new address object
var addresses = new List<Address>();
foreach (Match match in rawAddresses)
{
    // ...
}
```

### ❌ DON'T: Use List.Any() for deduplication checks

**Problem**: Inefficient O(n) check on each iteration.

```c#
var bins = new List<BinInfo>();

foreach (var row in rowsData.EnumerateObject())
{
    var serviceItemId = row.Value.GetProperty("ServiceItemID").GetString()!;

    if (bins.Any(b => b.ServiceItemId == serviceItemId))  // ❌ O(n) each time
    {
        continue;
    }

    bins.Add(new BinInfo { ServiceItemId = serviceItemId });
}
```

### ✅ DO: Use HashSet for deduplication

**Reason**: O(1) lookup performance.

```c#
var bins = new List<BinInfo>();
var seenServiceItemIds = new HashSet<string>();

foreach (var row in rowsData.EnumerateObject())
{
    var serviceItemId = row.Value.GetProperty("ServiceItemID").GetString()!;

    if (!seenServiceItemIds.Add(serviceItemId))  // ✅ O(1) and adds in one step
    {
        continue;
    }

    bins.Add(new BinInfo { ServiceItemId = serviceItemId });
}
```

### ❌ DON'T: Use .ToList().AsReadOnly()

**Problem**: Old pattern for creating readonly collections.

```c#
Addresses = addresses.ToList().AsReadOnly(),
```

### ✅ DO: Use collection expressions

**Reason**: Modern C# 12 syntax is more concise.

```c#
Addresses = [.. addresses],
```

---

## URL Encoding

### ❌ DON'T: Manually URL encode values

**Problem**: Using `WebUtility.UrlEncode` or manually encoding characters.

```c#
using System.Net;

var encodedPostcode = WebUtility.UrlEncode(postcode);  // ❌ Don't do this
var url = $"https://api.example.com?postcode={encodedPostcode}";

// Or manually replacing characters
var encodedValue = value.Replace(" ", "%20").Replace("&", "%26");  // ❌ Don't do this
```

### ✅ DO: Let the framework handle encoding

**Reason**: String interpolation in URLs and the Uri constructor handle encoding automatically.

```c#
// ✅ Just use the value directly
var url = $"https://api.example.com?postcode={postcode}";
var uri = new Uri(url);  // Uri constructor handles encoding

// ✅ In request bodies (JSON), values are automatically encoded
var body = $$"""
{
    "postcode": "{{postcode}}",
    "address": "{{address}}"
}
""";
```

**When manual encoding IS needed (rare):**

Only when an API explicitly requires pre-encoded values in a non-standard way. Always document why in a comment.

---

# Part 3: Templates & Testing

## Code Examples & Templates

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
			Name = "General Waste",
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
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = "https://www.mynewcouncil.gov.uk/bin-collections",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
			};

			var getAddressesResponse = new GetAddressesResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getAddressesResponse;
		}
		// Process addresses from response
		else if (clientSideResponse.RequestId == 1)
		{
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
			var clientSideRequest = new ClientSideRequest
			{
				RequestId = 1,
				Url = $"https://www.mynewcouncil.gov.uk/bin-collections?uprn={address.Uid}",
				Method = "GET",
				Headers = new()
				{
					{ "user-agent", Constants.UserAgent },
				},
			};

			var getBinDaysResponse = new GetBinDaysResponse
			{
				NextClientSideRequest = clientSideRequest,
			};

			return getBinDaysResponse;
		}
		// Process bin days from response
		else if (clientSideResponse.RequestId == 1)
		{
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

For collectors using shared vendor platforms:

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
			Name = "General Waste",
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

**Note**: No `GetAddresses()` or `GetBinDays()` methods—handled by the base class. Class is `sealed` but **not** `partial` (no regex methods).

---

## Testing Requirements

### Integration Tests

- **Requirement**: Every new collector **must** have an accompanying integration test
- **Test Helper**: Use the `TestSteps.EndToEnd` helper for end-to-end testing
- **Test Naming**: Test methods should be named descriptively (e.g. `GetBinDaysTest`)
- **Test File Location**: `BinDays.Api.IntegrationTests/Collectors/Councils/[CollectorName]Tests.cs`

### Example Integration Test Template

```c#
namespace BinDays.Api.IntegrationTests.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Councils;
using BinDays.Api.IntegrationTests.Helpers;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class MyNewCouncilTests
{
	private readonly IntegrationTestClient _client;
	private readonly ITestOutputHelper _outputHelper;
	private static readonly string _govUkId = new MyNewCouncil().GovUkId;

	public MyNewCouncilTests(ITestOutputHelper outputHelper)
	{
		_outputHelper = outputHelper;
		_client = new IntegrationTestClient(outputHelper);
	}

	[Theory]
	[InlineData("ABCD EFG")]
	public async Task GetBinDaysTest(string postcode)
	{
		await TestSteps.EndToEnd(
			_client,
			postcode,
			_govUkId,
			_outputHelper
		);
	}
}
```

---

## Quick Reference Checklist

Before submitting a PR, check:

**Structure:**

- [ ] File in correct directory (`Collectors/Councils/`)
- [ ] Class name matches filename
- [ ] `internal sealed` (add `partial` only if using `[GeneratedRegex]`)
- [ ] Integration test created in `IntegrationTests/Collectors/Councils/`

**HTTP & Requests:**

- [ ] HTTP headers are minimal (typically just `user-agent` and `content-type`)
- [ ] Request bodies contain only required fields (no empty/null/default/false values)
- [ ] Use raw string literals for JSON, not nested dictionaries
- [ ] Use `new()` for dictionaries, not `new Dictionary<string, string>()`
- [ ] Use HTTPS URLs when available

**Bin Configuration:**

- [ ] Bin names are descriptive (what goes in, not just color/type)
- [ ] Bin type keys include only what's actually matched
- [ ] No bin names extracted as constants
- [ ] No explicit `Type = BinType.Bin` (it's the default)
- [ ] Bin types declared before const fields in configuration section

**Data Handling:**

- [ ] Postcode is NOT formatted in collector (it comes pre-formatted)
- [ ] Postcode NOT extracted from address data - use parameter
- [ ] Metadata contains only values needed in subsequent requests
- [ ] No explicit empty metadata initialization
- [ ] Required values use null-forgiving operator (`!`) not null-coalescing (`??`)
- [ ] Headers accessed with direct indexer for required headers
- [ ] JSON properties use `GetString()!` for required values
- [ ] No URL encoding - let framework handle it automatically

**Code Style:**

- [ ] All multi-line initializers have trailing commas
- [ ] Date parsing uses multi-line format
- [ ] Closing parenthesis on separate line for multi-line calls
- [ ] No unused using statements (especially System.Web, System.Linq if not used)
- [ ] No inline returns - use separate variable declarations
- [ ] `partial` keyword only for classes with `[GeneratedRegex]`

**Regex:**

- [ ] Use named capture groups, access by name not index
- [ ] Use null-forgiving operator `!` on `Matches()` calls
- [ ] No unused `[GeneratedRegex]` methods
- [ ] Use `[GeneratedRegex]` for all patterns - no `Regex.Match/Replace` with strings

**Complexity & Structure:**

- [ ] Collector is under 500 lines (ideally 200-400)
- [ ] No unnecessary retry logic or over-engineering
- [ ] No defensive fallbacks for cases that shouldn't occur

**GetBinDays:**

- [ ] Don't re-fetch address data - use `address.Uid` directly
- [ ] Only re-fetch if API strictly requires it (document why)

**Helpers & Patterns:**

- [ ] No single-use helper methods
- [ ] All helper methods have XML documentation
- [ ] Duplicate logic (2-3+ uses) extracted to helpers
- [ ] Standard iteration comment before foreach loops
- [ ] Use HashSet for deduplication, not `List.Any()`
- [ ] Use collection expressions `[.. collection]`, not `.ToList().AsReadOnly()`

**Constants:**

- [ ] No constants for single-use values
- [ ] Constants only for values used 2+ times

**Address:**

- [ ] Use `string.Join` with `Where` for address building, not if statements
- [ ] Use switch expressions, not nested ternary operators
- [ ] Don't split addresses into Street/Town unless API requires it
- [ ] Don't sort addresses - return in order received
- [ ] Trim all related values consistently
- [ ] Concatenate multiple data parts into UID using semicolon (`;`) separator when GetBinDays needs multiple values
- [ ] Split UID with `address.Uid!.Split(';')` in GetBinDays to extract the parts
- [ ] Document UID format with comments when using concatenation

---

## Summary

The key principle behind these patterns is: **Keep collectors simple, minimal, and intentionally brittle**.

- Use the minimum necessary code
- Let failures happen loudly and clearly
- Don't add defensive logic for cases that shouldn't occur
- Only extract/abstract when there's genuine duplication (2-3+ uses)
- Follow modern C# conventions (collection expressions, target-typed new, etc.)
- Be consistent (trailing commas, trimming, formatting)
- Target 200-400 lines per collector

When in doubt, look at existing approved collectors for reference patterns.
