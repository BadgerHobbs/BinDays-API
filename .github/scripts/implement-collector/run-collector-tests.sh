#!/bin/bash
set -o pipefail

# Run integration tests for a collector and extract the summary
#
# Required environment variables:
#   COLLECTOR_NAME - Council name in PascalCase
#   GITHUB_ENV - Path to GitHub Actions environment file

echo "Running integration tests for ${COLLECTOR_NAME}..."
dotnet test --filter "FullyQualifiedName~${COLLECTOR_NAME}Tests.GetBinDaysTest" \
  --logger "console;verbosity=detailed" \
  BinDays.Api.IntegrationTests/BinDays.Api.IntegrationTests.csproj | tee test_output.txt

# Extract summary
SUMMARY=$(sed -n '/==================== Test Summary ====================/,/======================================================/p' test_output.txt)

if [ -z "$SUMMARY" ]; then
  SUMMARY="Test summary could not be extracted."
fi

echo "TEST_SUMMARY<<EOF" >> $GITHUB_ENV
echo "$SUMMARY" >> $GITHUB_ENV
echo "EOF" >> $GITHUB_ENV
