#!/bin/bash
set -e

# Implement a collector with retry logic
#
# Runs Codex to implement a collector, then verifies the files exist.
# If verification fails, resumes the previous Codex session to complete
# any unfinished work.
#
# Required environment variables:
#   ISSUE_TITLE - Council name from issue title
#   ISSUE_NUMBER - GitHub issue number
#   COLLECTOR_NAME - Council name in PascalCase
#
# Optional environment variables:
#   MAX_ATTEMPTS - Maximum number of attempts (default: 3)
#   BINDAYS_ENABLE_HTTP_LOGGING - Enable HTTP logging

MAX_ATTEMPTS="${MAX_ATTEMPTS:-3}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

COLLECTOR_FILE="BinDays.Api.Collectors/Collectors/Councils/${COLLECTOR_NAME}.cs"
TEST_FILE="BinDays.Api.IntegrationTests/Collectors/Councils/${COLLECTOR_NAME}Tests.cs"

RESUME_PROMPT="You stopped before completing all steps. This is a non-interactive CI/CD task with no user present. Please continue the implementation of ${COLLECTOR_NAME} until completion."

# First attempt - run fresh
echo "=========================================="
echo "Attempt 1 of $MAX_ATTEMPTS"
echo "=========================================="

"$SCRIPT_DIR/implement-collector.sh"

if "$SCRIPT_DIR/verify-implementation.sh"; then
  echo "Implementation verified successfully"
  exit 0
fi

# Subsequent attempts - resume previous session
for attempt in $(seq 2 "$MAX_ATTEMPTS"); do
  echo "=========================================="
  echo "Attempt $attempt of $MAX_ATTEMPTS (resuming)"
  echo "=========================================="

  codex exec resume --last --skip-git-repo-check --dangerously-bypass-approvals-and-sandbox "$RESUME_PROMPT"

  if "$SCRIPT_DIR/verify-implementation.sh"; then
    echo "Implementation verified successfully after resume"
    exit 0
  fi

  echo "Verification failed on attempt $attempt"
done

echo "All $MAX_ATTEMPTS attempts exhausted. Giving up."
exit 1
