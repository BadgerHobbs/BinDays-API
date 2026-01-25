/**
 * Report implementation failure by commenting on the issue.
 *
 * Required environment variables:
 *   ISSUE_NUMBER - GitHub issue number
 *   RUN_ID - GitHub Actions run ID
 *
 * @param {Object} github - GitHub API client
 * @param {Object} context - GitHub Actions context
 * @param {Object} core - GitHub Actions core utilities
 */
module.exports = async ({ github, context, core }) => {
  const issueNumber = parseInt(process.env.ISSUE_NUMBER);
  const runId = process.env.RUN_ID || context.runId;
  const runUrl = `https://github.com/${context.repo.owner}/${context.repo.repo}/actions/runs/${runId}`;

  await github.rest.issues.createComment({
    owner: context.repo.owner,
    repo: context.repo.repo,
    issue_number: issueNumber,
    body: `**Moley-Bot here!** Unfortunately, the automated collector implementation failed. Please check the [workflow run](${runUrl}) for details.

Common issues:
- Council website may have changed or is using an unsupported pattern
- The provided postcode may not return valid results
- Network/timeout issues during website navigation

A developer may need to implement this collector manually.`
  });
};
