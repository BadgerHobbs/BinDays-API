# Debugging Integration Tests

This document provides instructions on how to enable detailed HTTP request and response logging for the integration tests. This is useful for debugging issues with bin collectors, especially when developing new ones.

## Enabling HTTP Logging

To enable HTTP logging, you need to set the `BINDAYS_ENABLE_HTTP_LOGGING` environment variable to `true`. When this variable is set, the integration tests will output detailed information about each HTTP request and response to the test output.

### Running Tests with Logging in PowerShell

You can run a specific test with logging enabled using the following command in PowerShell:

```powershell
$env:BINDAYS_ENABLE_HTTP_LOGGING="true"
dotnet test --filter "FullyQualifiedName~WestDevonBoroughCouncilTests.GetBinDaysTest" --nologo --logger "console;verbosity=detailed"
```

### Running Tests with Logging in CMD

If you are using the Command Prompt, you can use the following command:

```cmd
set BINDAYS_ENABLE_HTTP_LOGGING=true
dotnet test --filter "FullyQualifiedName~WestDevonBoroughCouncilTests.GetBinDaysTest" --nologo --logger "console;verbosity=detailed"
```

### Running Tests with Logging in Bash

If you are using Bash (e.g., Git Bash, WSL), you can use the following commands:

```bash
export BINDAYS_ENABLE_HTTP_LOGGING="true"
dotnet test --filter "FullyQualifiedName~WestDevonBoroughCouncilTests.GetBinDaysTest" --nologo --logger "console;verbosity=detailed"
```

After running the tests, you can disable logging by closing the terminal or by setting the environment variable to `false`:

```powershell
$env:BINDAYS_ENABLE_HTTP_LOGGING="false"
```

```cmd
set BINDAYS_ENABLE_HTTP_LOGGING=false
```

```bash
export BINDAYS_ENABLE_HTTP_LOGGING="false"
```