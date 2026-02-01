# BinDays-API

.NET 8.0 API that returns bin collection schedules for UK councils. Collectors replicate council website HTTP requests; the actual requests are executed client-side by the mobile app, so the API is stateless.

## Architecture

- **BinDays.Api/** -- ASP.NET Web API entry point
- **BinDays.Api.Collectors/** -- Collector implementations (one class per council) and shared utilities
  - `Collectors/Councils/` -- individual council collectors
  - `Collectors/Vendors/` -- shared base classes for common vendor platforms
  - `Models/` -- domain models (`Bin`, `BinDay`, `Address`, `ClientSideRequest`, etc.)
  - `Utilities/` -- `ProcessingUtilities`, `Constants`, extension methods
- **BinDays.Api.IntegrationTests/** -- per-collector integration tests

## Key conventions

- **Sealed classes** -- all collectors are `internal sealed` (add `partial` only when using `[GeneratedRegex]`)
- **Expression-bodied members** -- use `=>` for property getters
- **Collection expressions** -- use `[.. items]` instead of `.ToList().AsReadOnly()`
- **Target-typed new** -- `new()` for dictionaries, `new("url")` for Uri
- **Trailing commas** -- on every last element in multi-line initialisers
- **Fail fast** -- no try/catch around parsing; use `!` (null-forgiving) for required values
- **Minimal HTTP headers** -- typically just `user-agent` and `content-type`
- **Raw string literals** -- for JSON request bodies

See `.gemini/styleguide.md` for the full style guide with do's and don'ts.

## Build and test

```bash
dotnet build
dotnet test
dotnet format --severity info
```
