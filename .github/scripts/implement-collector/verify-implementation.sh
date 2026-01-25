#!/bin/bash
set -e

# Verify that collector implementation files were created
#
# Required environment variables:
#   COLLECTOR_NAME - Council name in PascalCase

echo "Checking for implementation files..."

if [ ! -f "BinDays.Api.Collectors/Collectors/Councils/${COLLECTOR_NAME}.cs" ]; then
  echo "Error: Collector class file not found"
  exit 1
fi

if [ ! -f "BinDays.Api.IntegrationTests/Collectors/Councils/${COLLECTOR_NAME}Tests.cs" ]; then
  echo "Error: Test class file not found"
  exit 1
fi

echo "Implementation files verified successfully"
