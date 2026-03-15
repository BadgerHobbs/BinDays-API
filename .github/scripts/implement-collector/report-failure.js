/**
 * Report implementation failure by commenting on the issue.
 *
 * Required environment variables:
 *   ISSUE_NUMBER      - GitHub issue number
 *   FAILURE_SUMMARY   - AI-generated summary of what went wrong (optional)
 *
 * @param {Object} github - GitHub API client
 * @param {Object} context - GitHub Actions context
 * @param {Object} core - GitHub Actions core utilities
 */
module.exports = async ({ github, context, core }) => {
  const issueNumber = parseInt(process.env.ISSUE_NUMBER);
  const failureSummary = process.env.FAILURE_SUMMARY || '';
  const runId = context.runId;
  const runUrl = `https://github.com/${context.repo.owner}/${context.repo.repo}/actions/runs/${runId}`;

  let body = '**Moley-Bot here!** Unfortunately, the automated collector implementation failed.\n\n';

  if (failureSummary) {
    body += `**What went wrong:** ${failureSummary}\n\n`;
  }

  body += `[View workflow run](${runUrl})`;

  await github.rest.issues.createComment({
    owner: context.repo.owner,
    repo: context.repo.repo,
    issue_number: issueNumber,
    body,
  });
};
