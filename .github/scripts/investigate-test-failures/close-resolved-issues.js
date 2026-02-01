/**
 * Close collector-broken issues for councils whose tests now pass.
 *
 * Lists jobs from the triggering workflow run, identifies councils that
 * passed, and closes any matching open "collector-broken" issues.
 *
 * Required environment variables:
 *   WORKFLOW_RUN_ID - The ID of the triggering workflow run
 *
 * @param {Object} github - GitHub API client
 * @param {Object} context - GitHub Actions context
 * @param {Object} core - GitHub Actions core utilities
 */
module.exports = async ({ github, context, core }) => {
  const runId = parseInt(process.env.WORKFLOW_RUN_ID);
  const runUrl = `https://github.com/${context.repo.owner}/${context.repo.repo}/actions/runs/${runId}`;

  // List all jobs for the triggering workflow run
  const jobs = [];
  for (let page = 1; ; page++) {
    const { data } = await github.rest.actions.listJobsForWorkflowRun({
      owner: context.repo.owner,
      repo: context.repo.repo,
      run_id: runId,
      per_page: 100,
      page,
    });
    jobs.push(...data.jobs);
    if (jobs.length >= data.total_count) break;
  }

  // Find councils whose tests passed
  const passingCouncils = new Set(
    jobs
      .filter(job => job.conclusion === 'success' && job.name.startsWith('Test '))
      .map(job => job.name.replace(/^Test /, ''))
  );

  core.info(`Found ${passingCouncils.size} passing council(s)`);

  if (passingCouncils.size === 0) {
    core.info('No passing councils — nothing to close');
    return;
  }

  // List all open issues with the "collector-broken" label
  const { data: openIssues } = await github.rest.issues.listForRepo({
    owner: context.repo.owner,
    repo: context.repo.repo,
    labels: 'collector-broken',
    state: 'open',
    per_page: 100,
  });

  let closedCount = 0;
  for (const issue of openIssues) {
    const match = issue.title.match(/^Broken collector:\s*(.+)$/);
    if (!match) continue;

    const councilName = match[1].trim();
    if (!passingCouncils.has(councilName)) continue;

    core.info(`Closing issue #${issue.number} — ${councilName} is passing again`);

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

  core.info(`Closed ${closedCount} issue(s)`);
};
