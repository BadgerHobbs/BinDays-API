#!/bin/bash
set -e

# Discover and verify collector implementation files.
#
# Instead of relying on a predicted class name, this script finds new .cs files
# that Codex actually created in the Councils directories, extracts the collector
# name from the filename, and verifies both collector and test files exist.
#
# Prints the discovered collector name to stdout (diagnostics go to stderr).
# Exit code 0 = success, 1 = verification failed.

COLLECTORS_DIR="BinDays.Api.Collectors/Collectors/Councils"
TESTS_DIR="BinDays.Api.IntegrationTests/Collectors/Councils"

echo "Discovering new collector files..." >&2

# Find new (untracked or staged-as-new) .cs files in the Councils collectors directory
NEW_FILES=$(git status --porcelain "$COLLECTORS_DIR" | grep -E '^\?\?|^A ' | grep '\.cs$' | sed 's/^...//' || true)

if [ -z "$NEW_FILES" ]; then
  echo "Error: No new collector files found in $COLLECTORS_DIR" >&2
  exit 1
fi

# Count how many new collector files we found
FILE_COUNT=$(echo "$NEW_FILES" | wc -l | tr -d ' ')
if [ "$FILE_COUNT" -gt 1 ]; then
  echo "Warning: Found $FILE_COUNT new collector files, using the first one:" >&2
  echo "$NEW_FILES" >&2
fi

# Take the first file and extract the class name from the filename
COLLECTOR_FILE=$(echo "$NEW_FILES" | head -n 1)
COLLECTOR_NAME=$(basename "$COLLECTOR_FILE" .cs)

echo "Discovered collector: $COLLECTOR_NAME" >&2

# Verify the collector file exists
if [ ! -f "$COLLECTORS_DIR/${COLLECTOR_NAME}.cs" ]; then
  echo "Error: Collector class file not found at $COLLECTORS_DIR/${COLLECTOR_NAME}.cs" >&2
  exit 1
fi

# Verify the test file exists
if [ ! -f "$TESTS_DIR/${COLLECTOR_NAME}Tests.cs" ]; then
  echo "Error: Test class file not found at $TESTS_DIR/${COLLECTOR_NAME}Tests.cs" >&2
  exit 1
fi

echo "Implementation files verified successfully" >&2

# Output the discovered name to stdout for capture by the caller
echo "$COLLECTOR_NAME"
