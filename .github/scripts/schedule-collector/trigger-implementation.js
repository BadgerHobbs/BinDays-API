/**
 * Trigger collector implementation by posting /implement comment on the issue.
 *
 * Required environment variables:
 *   ISSUE_NUMBER - GitHub issue number
 *   ISSUE_TITLE - Issue title
 *
 * @param {Object} github - GitHub API client
 * @param {Object} context - GitHub Actions context
 * @param {Object} core - GitHub Actions core utilities
 */
module.exports = async ({ github, context, core }) => {
  const issueNumber = parseInt(process.env.ISSUE_NUMBER);
  const issueTitle = process.env.ISSUE_TITLE;

  core.info(`Triggering implementation for issue #${issueNumber}: ${issueTitle}`);

  // Post /implement comment to trigger the implement-collector workflow
  await github.rest.issues.createComment({
    owner: context.repo.owner,
    repo: context.repo.repo,
    issue_number: issueNumber,
    body: '/implement'
  });

  core.info(`Posted /implement comment on issue #${issueNumber}`);

  // Add a summary
  await core.summary
    .addHeading('Scheduled Collector Implementation')
    .addTable([
      [{data: 'Issue', header: true}, {data: 'Title', header: true}],
      [`#${issueNumber}`, issueTitle]
    ])
    .addLink('View Issue', `https://github.com/${context.repo.owner}/${context.repo.repo}/issues/${issueNumber}`)
    .write();
};
