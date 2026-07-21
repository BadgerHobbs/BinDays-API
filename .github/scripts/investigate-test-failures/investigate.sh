#!/bin/bash
set -e

# Run Codex to investigate integration test failures.
#
# Reads the prompt template from .agent/prompts/investigate-test-failures.md,
# substitutes the style guide, and invokes Codex CLI. After each attempt,
# verifies that an open "Broken collector: {council}" issue exists for every
# council needing investigation, retrying Codex (scoped to the remaining
# councils) if any are missing. Fails if issues are still missing after all
# attempts, so a silent Codex no-op cannot pass the workflow.
#
# Required environment variables:
#   GH_TOKEN — token for the gh CLI (failure-context.json must exist in the repo root)

MAX_ATTEMPTS=3

# Read the style guide for injection
STYLE_GUIDE=$(cat .gemini/styleguide.md)

# Read the prompt template and substitute variables
PROMPT=$(cat .agent/prompts/investigate-test-failures.md)
PROMPT="${PROMPT//\$STYLE_GUIDE/$STYLE_GUIDE}"

# Councils that need investigation, from failure-context.json.
# Captured via command substitution (not process substitution) so a node
# failure, e.g. missing or malformed JSON, is caught by set -e.
COUNCILS_TEXT=$(node -e "
  const ctx = require('./failure-context.json');
  for (const f of ctx.failures) {
    if (f.needsInvestigation) console.log(f.councilName);
  }
")

if [ -n "$COUNCILS_TEXT" ]; then
  readarray -t COUNCILS <<< "$COUNCILS_TEXT"
else
  COUNCILS=()
fi

if [ ${#COUNCILS[@]} -eq 0 ]; then
  echo "No councils need investigation; nothing to do."
  exit 0
fi

# Populate MISSING_COUNCILS with councils that have no open tracking issue.
# gh issue list can lag behind a just-created issue (read-after-write delay),
# so a council isn't confirmed missing until it's absent across a few polls.
check_missing() {
  local open_titles

  for poll in 1 2 3; do
    open_titles=$(gh issue list --label collector-broken --state open --limit 100 --json title --jq '.[].title')

    MISSING_COUNCILS=()
    for council in "${COUNCILS[@]}"; do
      if ! grep -qxF "Broken collector: ${council}" <<< "$open_titles"; then
        MISSING_COUNCILS+=("$council")
      fi
    done

    if [ ${#MISSING_COUNCILS[@]} -eq 0 ] || [ "$poll" -eq 3 ]; then
      break
    fi

    sleep 5
  done
}

MISSING_COUNCILS=("${COUNCILS[@]}")

for attempt in $(seq 1 "$MAX_ATTEMPTS"); do
  ATTEMPT_PROMPT="$PROMPT"

  # On retries, scope the prompt to the councils still missing issues
  if [ "$attempt" -gt 1 ]; then
    ATTEMPT_PROMPT="$PROMPT

## Retry Notice

This is retry attempt ${attempt}. A previous attempt ended without creating all required issues.
Only investigate and create issues for these councils: ${MISSING_COUNCILS[*]}.
Do not create issues for any other council; they already have open issues."
  fi

  echo "Running Codex to investigate test failures (attempt ${attempt}/${MAX_ATTEMPTS})..."
  codex exec --skip-git-repo-check --dangerously-bypass-approvals-and-sandbox "$ATTEMPT_PROMPT" || echo "Codex exited with an error; verifying issues anyway..."

  check_missing

  if [ ${#MISSING_COUNCILS[@]} -eq 0 ]; then
    echo "All expected issues exist."
    exit 0
  fi

  echo "Missing issues for: ${MISSING_COUNCILS[*]}"
done

echo "::error::Codex failed to create issues for: ${MISSING_COUNCILS[*]}"
exit 1
