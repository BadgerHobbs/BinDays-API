/**
 * Create a pull request for the implemented collector and comment on the issue.
 *
 * Required environment variables:
 *   COUNCIL_NAME - Council name from issue title
 *   COUNCIL_NAME_PASCAL - Council name in PascalCase
 *   BRANCH_NAME - Git branch name
 *   ISSUE_NUMBER - GitHub issue number
 *   TEST_SUMMARY - Test execution summary
 *
 * @param {Object} github - GitHub API client
 * @param {Object} context - GitHub Actions context
 * @param {Object} core - GitHub Actions core utilities
 */
module.exports = async ({ github, context, core }) => {
  const councilName = process.env.COUNCIL_NAME;
  const councilNamePascal = process.env.COUNCIL_NAME_PASCAL;
  const branchName = process.env.BRANCH_NAME;
  const issueNumber = parseInt(process.env.ISSUE_NUMBER);
  const testSummary = process.env.TEST_SUMMARY;

  const { data: pr } = await github.rest.pulls.create({
    owner: context.repo.owner,
    repo: context.repo.repo,
    title: `Add collector for ${councilName}`,
    head: branchName,
    base: 'main',
    body: `## Summary

This PR adds a new bin collection data collector for **${councilName}**.

- Implements \`ICollector\` interface
- Adds integration tests
- Successfully tested with example postcode from issue

Closes #${issueNumber}

## Test Summary

\`\`\`text
${testSummary}
\`\`\`

---

Generated automatically by **Moley-Bot** using Codex CLI`
  });

  core.info(`Created PR #${pr.number}: ${pr.html_url}`);

  // Add "new collector" label to the PR
  await github.rest.issues.addLabels({
    owner: context.repo.owner,
    repo: context.repo.repo,
    issue_number: pr.number,
    labels: ['new collector']
  });

  core.info(`Added "new collector" label to PR #${pr.number}`);

  // Comment on the issue with the PR link
  await github.rest.issues.createComment({
    owner: context.repo.owner,
    repo: context.repo.repo,
    issue_number: issueNumber,
    body: `**Moley-Bot here!** Collector implementation complete! Created PR #${pr.number}`
  });
};
