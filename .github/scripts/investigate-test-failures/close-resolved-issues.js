/**
 * Close "collector broken" issues (and their associated PRs) for councils
 * whose tests now pass.
 *
 * Reads the test-results.json artifact (downloaded by a prior workflow step)
 * to identify passing councils, then closes any matching open
 * "collector broken" issues along with any open pull requests that
 * reference those issues.
 *
 * Required environment variables:
 *   WORKFLOW_RUN_ID - The ID of the triggering workflow run
 *
 * @param {Object} github - GitHub API client
 * @param {Object} context - GitHub Actions context
 * @param {Object} core - GitHub Actions core utilities
 */
module.exports = async ({ github, context, core }) => {
  const fs = require('fs');
  const runId = parseInt(process.env.WORKFLOW_RUN_ID, 10);
  const runUrl = `https://github.com/${context.repo.owner}/${context.repo.repo}/actions/runs/${runId}`;

  if (!fs.existsSync('test-results.json')) {
    core.info('No test-results.json found — artifact may not have been uploaded');
    return;
  }

  const results = JSON.parse(fs.readFileSync('test-results.json', 'utf8'));

  const passingCouncils = new Set(results.passed);
  core.info(`Found ${passingCouncils.size} passing council(s)`);

  if (passingCouncils.size === 0) {
    core.info('No passing councils — nothing to close');
    return;
  }

  // List all open issues with the "collector broken" label
  const { data: openIssues } = await github.rest.issues.listForRepo({
    owner: context.repo.owner,
    repo: context.repo.repo,
    labels: 'collector broken',
    state: 'open',
    per_page: 100,
  });

  let closedCount = 0;
  let closedPrCount = 0;
  for (const issue of openIssues) {
    const match = issue.title.match(/^Broken collector:\s*(.+)$/);
    if (!match) continue;

    const councilName = match[1].trim();
    if (!passingCouncils.has(councilName)) continue;

    core.info(`Closing issue #${issue.number} — ${councilName} is passing again`);

    // Close any open pull requests that reference this issue
    const { data: linkedPrs } = await github.rest.search.issuesAndPullRequests({
      q: `repo:${context.repo.owner}/${context.repo.repo} is:pr is:open ${issue.number} in:body`,
    });

    for (const pr of linkedPrs.items) {
      core.info(`Closing PR #${pr.number} — linked to resolved issue #${issue.number}`);

      await github.rest.issues.createComment({
        owner: context.repo.owner,
        repo: context.repo.repo,
        issue_number: pr.number,
        body: `Automatically closing — **${councilName}** integration tests are passing again, so this fix is no longer needed.\n\n[Workflow run](${runUrl})`,
      });

      await github.rest.pulls.update({
        owner: context.repo.owner,
        repo: context.repo.repo,
        pull_number: pr.number,
        state: 'closed',
      });

      closedPrCount++;
    }

    await github.rest.issues.createComment({
      owner: context.repo.owner,
      repo: context.repo.repo,
      issue_number: issue.number,
      body: `Automatically closing — **${councilName}** integration tests are passing again.\n\n[Workflow run](${runUrl})`,
    });

    await github.rest.issues.update({
      owner: context.repo.owner,
      repo: context.repo.repo,
      issue_number: issue.number,
      state: 'closed',
      state_reason: 'completed',
    });

    closedCount++;
  }

  core.info(`Closed ${closedCount} issue(s) and ${closedPrCount} pull request(s)`);
};
