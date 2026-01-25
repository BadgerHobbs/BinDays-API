#!/bin/bash
set -e

# Create a branch and commit the implemented collector files
#
# Required environment variables:
#   COLLECTOR_NAME - Council name in PascalCase
#   ISSUE_NUMBER - GitHub issue number
#   GITHUB_OUTPUT - Path to GitHub Actions output file

BRANCH_NAME="collector/${COLLECTOR_NAME}-issue-${ISSUE_NUMBER}-$(date +%s)"

git config user.name "Moley-Bot"
git config user.email "moley-bot@users.noreply.github.com"

git checkout -b "$BRANCH_NAME"
git add "BinDays.Api.Collectors/Collectors/Councils/${COLLECTOR_NAME}.cs"
git add "BinDays.Api.IntegrationTests/Collectors/Councils/${COLLECTOR_NAME}Tests.cs"

git commit -m "Add collector for ${COLLECTOR_NAME}

Closes #${ISSUE_NUMBER}

Generated with Codex CLI by Moley-Bot"

git push origin "$BRANCH_NAME"

echo "branch_name=$BRANCH_NAME" >> $GITHUB_OUTPUT
