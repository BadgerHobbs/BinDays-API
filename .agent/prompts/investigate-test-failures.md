# Task: Investigate Integration Test Failures

You are investigating integration test failures for the BinDays API project. Scheduled daily integration tests have failed, and you need to analyse each failure and create GitHub issues.

**IMPORTANT: This is a fully automated, non-interactive task running in a CI/CD pipeline.** There is NO user present to answer questions, provide clarification, or give approval. You cannot ask for help or confirmation - you must make all decisions autonomously and complete the entire task independently.

**Completion criteria:** Your task is complete ONLY when `gh issue create` has succeeded for every failure where `needsInvestigation` is `true`. Do not end your turn after describing a plan — execute it fully. An automated check runs after you finish and fails the pipeline if any expected issue is missing.

## Input

The file `failure-context.json` in the repository root contains:

- `runId`: the workflow run ID
- `runUrl`: link to the failed GitHub Actions workflow run
- `failures`: array of objects with `councilName`, `logs`, and `needsInvestigation`

Only process failures where `needsInvestigation` is `true`. Failures marked `false` already have open tracking issues.

## Investigation Process

For each failure where `needsInvestigation` is `true`:

### 1. Read Context

- Read the failure logs from the JSON entry
- Read the collector source at `BinDays.Api.Collectors/Collectors/Councils/{councilName}.cs`

### 2. Check the Council Website with Playwright

Before categorising based on logs alone, use the Playwright MCP browser to manually verify whether the council website is actually working. This is the most reliable signal.

Extract the council's base URL from the collector source (look for the first `Uri` or URL string). Then:

1. Navigate to the council's bin collection / waste services page
2. Attempt an address search using the postcode from the test logs (look for `postcode:` or similar in the logs), or try a generic local postcode if none is present
3. Check whether real addresses are returned in the results
4. Select an address and confirm that actual bin collection dates are displayed

Record the outcome:
- **Website working** — you were able to complete a full address search and see bin day results
- **Website broken** — the page failed to load, the search returned no results, an error was shown, or the page structure was unrecognisable

If the Playwright check cannot be completed (e.g. the URL cannot be determined), note this and fall back to log-based categorisation only.

After completing the check (regardless of outcome), take a screenshot of the final page state using the Playwright MCP `browser_take_screenshot` tool. Then save it under the council name so it can be attached to the issue:

```bash
LATEST=$(ls -t .agent/playwright/out/*.png 2>/dev/null | head -1)
[ -n "$LATEST" ] && cp "$LATEST" ".agent/playwright/out/{councilName}-website.png"
```

Replace `{councilName}` with the actual council name from the failure entry (e.g. `BarnetCouncil`).

### 3. Categorise the Failure

Use **both** the Playwright result and the error logs to determine which category fits:

- **Website down** — Playwright showed the website is broken/unavailable, OR logs show `HttpRequestException`, SSL errors, timeouts, 5xx status codes. The council website is temporarily unavailable. No code fix needed.
- **Website changed** — Playwright confirmed the website is working (addresses and bin days are visible), but the test still failed. This means the collector code no longer matches the website's current structure. Errors in logs: `InvalidOperationException`, `NullReferenceException`, `FormatException`, assertion failures (empty collections, regex not matching, unexpected HTML structure).
- **Data issue** — `BinDaysNotFoundException`, `AddressesNotFoundException` and Playwright shows the website working. The collector may be parsing data incorrectly, or the test address/postcode may no longer be valid.

The Playwright check takes precedence: if the website is clearly working end-to-end, the failure is code-side ("Website changed" or "Data issue") even if the logs look ambiguous.

### 4. Create a GitHub Issue

For every failure, create a GitHub issue with:

- **Title:** `Broken collector: {councilName}` (this exact format is required for deduplication — do not deviate)
- **Label:** `collector-broken`
- **Body:** use a markdown table for the key details, followed by any extra notes. Use this format:

  ```markdown
  | Field | Value |
  |-------|-------|
  | Category | {category} |
  | Key error | {key error message} |
  | Website check | {Working / Broken / Could not determine} |
  | Workflow run | [{runId}]({runUrl}) |

  {any additional notes, e.g. pattern across failures, what Playwright found}
  ```

Use the `gh` CLI to create issues. Write the body to a temp file and use `--body-file` to preserve newlines:

```bash
cat > /tmp/issue-body.md << 'EOF'
| Field | Value |
|-------|-------|
| Category | {category} |
| Key error | {key error message} |
| Website check | {Working / Broken / Could not determine} |
| Workflow run | [{runId}]({runUrl}) |

{any additional notes}
EOF
gh issue create --title "Broken collector: {councilName}" --label "collector-broken" --body-file /tmp/issue-body.md
```

## Important Notes

- If **all** failures are in the same category (e.g. all "Website down"), mention this pattern in the first issue body. It may indicate a runner or network problem rather than individual council issues.
- Include the Playwright finding in the issue body (e.g. "Website manually verified as working" or "Website appears to be down"), so that humans reading the issue have the full context.
