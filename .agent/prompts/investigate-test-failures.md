# Task: Investigate Integration Test Failures

You are investigating integration test failures for the BinDays API project. Scheduled daily integration tests have failed, and you need to analyse each failure, create GitHub issues, and attempt fixes where appropriate.

**IMPORTANT: This is a fully automated, non-interactive task running in a CI/CD pipeline.** There is NO user present to answer questions, provide clarification, or give approval. You cannot ask for help or confirmation - you must make all decisions autonomously and complete the entire task independently.

## Input

The file `failure-context.json` in the repository root contains:

- `runId`: the workflow run ID (use this to make branch names unique)
- `runUrl`: link to the failed GitHub Actions workflow run
- `failures`: array of objects with `councilName`, `jobUrl`, `logs`, and `needsInvestigation`

Only process failures where `needsInvestigation` is `true`. Failures marked `false` already have open tracking issues.

## Style Guide

When attempting code fixes, you must follow all project conventions:

$STYLE_GUIDE

## Investigation Process

For each failure where `needsInvestigation` is `true`:

### 1. Read Context

- Read the failure logs from the JSON entry
- Read the collector source at `BinDays.Api.Collectors/Collectors/Councils/{councilName}.cs`

### 2. Categorise the Failure

Determine which category fits:

- **Website down** — `HttpRequestException`, SSL errors, timeouts, 5xx status codes. The council website is temporarily unavailable. No code fix needed.
- **Website changed** — `InvalidOperationException`, `NullReferenceException`, `FormatException`, assertion failures (empty collections, regex not matching, unexpected HTML structure). The council changed their website and the collector needs updating.
- **Data issue** — `BinDaysNotFoundException`, `AddressesNotFoundException`. The council may have changed how addresses or collections are returned, or the test data may no longer be valid. May need a code fix.

### 3. Create a GitHub Issue

For every failure, create a GitHub issue with:

- **Title:** `Broken collector: {councilName}` (this exact format is required for deduplication — do not deviate)
- **Label:** `collector-broken`
- **Body:** include the failure category, the key error message from the logs, a link to the workflow run (`runUrl`), and a link to the specific job (`jobUrl`)

Use the `gh` CLI to create issues:

```bash
gh issue create --title "Broken collector: {councilName}" --label "collector-broken" --body "..."
```

### 4. Attempt Fix (Website Changed Only)

**Only for "Website changed" failures**, attempt to fix the collector:

1. Read `CLAUDE.md` for project overview
2. Study 1-2 similar collectors for reference patterns
3. Analyse the error and collector source to determine what changed
4. Fix the collector code
5. Create a branch named `fix/{councilName}-collector-{runId}` (include the `runId` from `failure-context.json` to keep branches unique across runs)
6. Commit, push, and open a PR referencing the issue

```bash
git checkout -b fix/{councilName}-collector-{runId}
# ... make changes ...
git add -A
git commit -m "Fix {councilName} collector for website changes"
git push -u origin fix/{councilName}-collector-{runId}
gh pr create --title "Fix {councilName} collector (run {runId})" --body "Fixes #{issueNumber}\n\n..."
```

Fixes are best-effort — the issue ensures manual follow-up if the auto-fix doesn't work. The PR will trigger existing integration tests CI for verification.

## Important Notes

- If **all** failures are in the same category (e.g. all "Website down"), mention this pattern in the first issue body. It may indicate a runner or network problem rather than individual council issues.
- Do **not** create fix PRs for "Website down" or "Data issue" failures — only create the tracking issue.
- You can only reason from logs and source code (you cannot run the tests or access the council websites). Keep fix attempts realistic.
