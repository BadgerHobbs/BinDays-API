#!/bin/bash
set -o pipefail

# Ask Codex to summarise why the collector implementation failed.
#
# Collects context from test output and git state, then prompts Codex
# for a concise, human-readable summary suitable for a GitHub issue comment.
#
# Outputs:
#   Sets step output "summary" via $GITHUB_OUTPUT.
#
# Required environment variables:
#   COLLECTOR_NAME - Council name in PascalCase
#   GITHUB_OUTPUT  - Path to GitHub Actions output file

CONTEXT=""

# Collect test output if available
if [ -f test_output.txt ]; then
  CONTEXT+="## Test output (last 100 lines)
$(tail -100 test_output.txt)

"
fi

# Collect git status to show what files were created/modified
CONTEXT+="## Git status
$(git status --short)

"

# Collect build errors if any
BUILD_OUTPUT=$(dotnet build --no-restore 2>&1 | tail -50) || true
if echo "$BUILD_OUTPUT" | grep -qi "error"; then
  CONTEXT+="## Build output (last 50 lines)
$BUILD_OUTPUT

"
fi

# Check if collector/test files exist
COLLECTORS_DIR="BinDays.Api.Collectors/Collectors/Councils"
TESTS_DIR="BinDays.Api.IntegrationTests/Collectors/Councils"

if [ -n "$COLLECTOR_NAME" ]; then
  if [ ! -f "$COLLECTORS_DIR/${COLLECTOR_NAME}.cs" ]; then
    CONTEXT+="## Missing files
Collector file not found: $COLLECTORS_DIR/${COLLECTOR_NAME}.cs
"
  fi
  if [ ! -f "$TESTS_DIR/${COLLECTOR_NAME}Tests.cs" ]; then
    CONTEXT+="## Missing files
Test file not found: $TESTS_DIR/${COLLECTOR_NAME}Tests.cs
"
  fi
fi

PROMPT="You are analysing a failed GitHub Actions run that tried to automatically implement a BinDays collector for ${COLLECTOR_NAME:-an unknown council}.

Here is the context from the failed run:

${CONTEXT}

Write a short summary (2-4 sentences) explaining why the implementation failed. Be specific: mention the actual error messages, missing files, or test failures. Do not suggest fixes. Do not use markdown formatting. Write in plain English as if commenting on a GitHub issue."

# Run Codex for the summary (short task, no sandbox needed)
SUMMARY=$(codex exec --skip-git-repo-check --dangerously-bypass-approvals-and-sandbox "$PROMPT" 2>/dev/null)

if [ -z "$SUMMARY" ]; then
  SUMMARY="Could not generate a failure summary. Please check the workflow logs."
fi

# Write to step output (handle multiline via delimiter)
{
  echo "summary<<SUMMARY_EOF"
  echo "$SUMMARY"
  echo "SUMMARY_EOF"
} >> "$GITHUB_OUTPUT"
