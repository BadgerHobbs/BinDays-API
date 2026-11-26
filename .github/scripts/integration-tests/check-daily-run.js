/**
 * Decide whether a scheduled integration test run should be skipped.
 *
 * GitHub's `schedule` trigger has been starting the daily run many hours late, so the daily run is
 * triggered on time through `workflow_dispatch` from an external scheduler, and the cron schedule is
 * only a fallback for days when that trigger does not fire. A scheduled run is therefore skipped when
 * a dispatched run has already been created today (UTC), so the suite does not run twice.
 *
 * Sets the `skip` output to 'true' or 'false'. Runs triggered by anything other than `schedule` are
 * never skipped.
 *
 * @param {Object} github - GitHub API client
 * @param {Object} context - GitHub Actions context
 * @param {Object} core - GitHub Actions core utilities
 */
module.exports = async ({ github, context, core }) => {
  if (context.eventName !== 'schedule') {
    core.setOutput('skip', 'false');
    return;
  }

  const { data: currentRun } = await github.rest.actions.getWorkflowRun({
    owner: context.repo.owner,
    repo: context.repo.repo,
    run_id: context.runId,
  });

  const today = new Date().toISOString().slice(0, 10);

  const { data: dispatchedRuns } = await github.rest.actions.listWorkflowRuns({
    owner: context.repo.owner,
    repo: context.repo.repo,
    workflow_id: currentRun.workflow_id,
    event: 'workflow_dispatch',
    created: `>=${today}`,
    per_page: 20,
  });

  const runToday = dispatchedRuns.workflow_runs.find((run) => run.conclusion !== 'cancelled');

  if (runToday) {
    core.info(`Skipping: dispatched run ${runToday.html_url} already ran today (created ${runToday.created_at}).`);
    core.setOutput('skip', 'true');
    return;
  }

  core.info('No dispatched run today, so this scheduled run is the fallback and will run the tests.');
  core.setOutput('skip', 'false');
};
