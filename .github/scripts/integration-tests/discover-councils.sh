#!/bin/bash
set -e

# Discover which council tests to run based on changed files
#
# This script determines whether to run all council tests or only tests
# for councils that have been modified. It's optimized for PR workflows
# to reduce test execution time when only specific councils are changed.
#
# Required environment variables:
#   GITHUB_EVENT_NAME - GitHub event type (pull_request, push, schedule, etc.)
#   GITHUB_BASE_REF - Base branch for pull requests
#   EXCLUDED_COUNCILS - JSON array of councils to exclude (e.g., '["Council1", "Council2"]')
#   GITHUB_OUTPUT - Path to GitHub Actions output file

RUN_ALL=true
CANDIDATE_COUNCILS=""

if [[ "$GITHUB_EVENT_NAME" == "pull_request" ]]; then
  echo "Pull Request detected. Checking for optimization opportunities..."

  # Use origin/main as default base if github.base_ref is somehow empty
  BASE_REF="origin/$GITHUB_BASE_REF"
  echo "Comparing HEAD against $BASE_REF"

  CHANGED_FILES=$(git diff --name-only "$BASE_REF" HEAD)

  COUNCIL_IMPL_PATTERN="^BinDays\.Api\.Collectors/Collectors/Councils/.*\.cs$"
  COUNCIL_TEST_PATTERN="^BinDays\.Api\.IntegrationTests/Collectors/Councils/.*Tests\.cs$"

  NON_COUNCIL_CHANGES=$(echo "$CHANGED_FILES" | grep -vE "$COUNCIL_IMPL_PATTERN|$COUNCIL_TEST_PATTERN" || true)

  if [[ -z "$NON_COUNCIL_CHANGES" && -n "$CHANGED_FILES" ]]; then
    echo "Only council files changed. Optimizing test run."
    RUN_ALL=false

    IMPL_NAMES=$(echo "$CHANGED_FILES" | grep -E "$COUNCIL_IMPL_PATTERN" | sed 's|BinDays.Api.Collectors/Collectors/Councils/||' | sed 's|\.cs$||' || true)
    TEST_NAMES=$(echo "$CHANGED_FILES" | grep -E "$COUNCIL_TEST_PATTERN" | sed 's|BinDays.Api.IntegrationTests/Collectors/Councils/||' | sed 's|Tests\.cs$||' || true)

    RAW_LIST=$(echo -e "$IMPL_NAMES\n$TEST_NAMES" | sort | uniq | grep -v "^$")

    for council in $RAW_LIST; do
      TEST_FILE="BinDays.Api.IntegrationTests/Collectors/Councils/${council}Tests.cs"
      if [[ -f "$TEST_FILE" ]]; then
        CANDIDATE_COUNCILS+="$council"$'\n'
      else
        echo "Skipping $council: Test file $TEST_FILE not found."
      fi
    done
  else
     echo "Changes detected outside of council specific files (or no changes). Running all tests."
     if [[ -n "$NON_COUNCIL_CHANGES" ]]; then
       echo "Sample non-council changes:"
       echo "$NON_COUNCIL_CHANGES" | head -n 5
     fi
  fi
fi

if [[ "$RUN_ALL" == "true" ]]; then
   TARGET_DIR="BinDays.Api.IntegrationTests/Collectors/Councils"
   if [[ -d "$TARGET_DIR" ]]; then
     CANDIDATE_COUNCILS=$(ls "$TARGET_DIR"/*Tests.cs | xargs -n 1 basename | sed 's/Tests\.cs$//')
   else
     echo "Error: Test directory not found!"
     exit 1
   fi
fi

JSON_ARRAY=$(echo "$CANDIDATE_COUNCILS" | grep -v "^$" | jq -R . | jq -s .)

if [[ "$JSON_ARRAY" == "null" ]]; then
   JSON_ARRAY="[]"
fi

FILTERED_JSON=$(echo "$JSON_ARRAY" | jq -c --argjson excluded "$EXCLUDED_COUNCILS" '. - $excluded')

echo "Final list of councils to test: $FILTERED_JSON"
echo "councils_json=$FILTERED_JSON" >> $GITHUB_OUTPUT
