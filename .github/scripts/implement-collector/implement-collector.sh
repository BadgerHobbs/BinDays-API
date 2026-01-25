#!/bin/bash
set -e

# Implement a collector using Codex CLI
#
# Required environment variables:
#   ISSUE_TITLE - Council name from issue title
#   ISSUE_NUMBER - GitHub issue number
#   DOTNET_ROOT - Path to .NET installation (optional)
#   BINDAYS_ENABLE_HTTP_LOGGING - Enable HTTP logging (optional)

# Ensure dotnet is in PATH for Codex subshells
# The setup-dotnet action sets DOTNET_ROOT and adds it to PATH
if [ -n "$DOTNET_ROOT" ]; then
  export PATH="$DOTNET_ROOT:$PATH"
fi

# Verify dotnet is accessible
echo "dotnet location: $(which dotnet)"
echo "dotnet version: $(dotnet --version)"

ISSUE_BODY=$(cat .agent/playwright/out/issue_body.txt)

# Read the style guide for injection
STYLE_GUIDE=$(cat .gemini/styleguide.md)

# Read the prompt template and substitute variables
PROMPT=$(cat .agent/prompts/implement-collector.md)
PROMPT="${PROMPT//\$ISSUE_TITLE/$ISSUE_TITLE}"
PROMPT="${PROMPT//\$ISSUE_BODY/$ISSUE_BODY}"
PROMPT="${PROMPT//\$ISSUE_NUMBER/$ISSUE_NUMBER}"
PROMPT="${PROMPT//\$STYLE_GUIDE/$STYLE_GUIDE}"

# Run Codex with the prompt
echo "Running Codex to implement collector..."
codex exec --skip-git-repo-check --dangerously-bypass-approvals-and-sandbox "$PROMPT"
