#!/bin/bash
set -e

# Run Codex to investigate integration test failures.
#
# Reads the prompt template from .agent/prompts/investigate-test-failures.md,
# substitutes the style guide, and invokes Codex CLI.
#
# Required environment variables:
#   (none — failure-context.json must exist in the repo root)

# Read the style guide for injection
STYLE_GUIDE=$(cat .gemini/styleguide.md)

# Read the prompt template and substitute variables
PROMPT=$(cat .agent/prompts/investigate-test-failures.md)
PROMPT="${PROMPT//\$STYLE_GUIDE/$STYLE_GUIDE}"

# Run Codex with the prompt
echo "Running Codex to investigate test failures..."
codex exec --skip-git-repo-check --dangerously-bypass-approvals-and-sandbox "$PROMPT"
