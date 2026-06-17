# BinDays-API

.NET 10.0 API that returns bin collection schedules for UK councils. Collectors replicate council website HTTP requests; the actual requests are executed client-side by the mobile app, so the API is stateless.

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

## Prerequisites

- .NET SDK 10.0+
- Dart SDK 3.7+ (for integration tests)
- Network access and the system `tar` (for the libcurl-impersonate native library download below; `tar` ships with Windows 10 1803+, Linux and macOS)

## Build and test

```bash
dotnet build
dotnet test
dotnet format --severity info
```

The first `dotnet build` automatically compiles the Dart CLI wrapper (`BinDays.Api.IntegrationTests/DartClient/`) via an MSBuild target. This requires the Dart SDK to be installed. Delete `DartClient/bin/send_request.exe` to force a recompile.

The same build step also provisions the `libcurl-impersonate` shared library (via `dart run bindays_client:install`) into `BinDays.Api.IntegrationTests/DartClient/.native/` (gitignored). `bindays_client`'s default transport loads it so every client-side request — in the tests and in the app alike — presents a real browser's TLS/HTTP-2 fingerprint, which is required for councils behind a Cloudflare TLS-fingerprint challenge. The transport is always on (no per-test flag); the tests and the app share one code path. Delete `DartClient/bin/send_request.exe` to force a recompile and re-provision.
