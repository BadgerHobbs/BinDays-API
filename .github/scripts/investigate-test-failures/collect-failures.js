/**
 * Collect failure information from a completed integration tests workflow run.
 *
 * Calls the GitHub Actions API to list jobs for the triggering workflow run,
 * filters to failed test jobs, downloads their logs, and writes a JSON file
 * with all failure context for downstream investigation.
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

  core.info(`Collecting failures from workflow run ${runId}`);

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

  core.info(`Found ${jobs.length} total job(s) in the workflow run`);

  // Filter to failed test jobs (job names are "Test {CouncilName}")
  const failedJobs = jobs.filter(
    job => job.conclusion === 'failure' && job.name.startsWith('Test ')
  );

  core.info(`Found ${failedJobs.length} failed test job(s)`);

  if (failedJobs.length === 0) {
    core.setOutput('has_failures', 'false');
    core.setOutput('failure_count', '0');
    core.setOutput('run_url', runUrl);
    return;
  }

  // Download logs for each failed job and extract council name
  const failures = [];
  for (const job of failedJobs) {
    const councilName = job.name.replace(/^Test /, '');
    core.info(`Downloading logs for ${councilName} (job ${job.id})`);

    let logs = '';
    try {
      const { data } = await github.rest.actions.downloadJobLogsForWorkflowRun({
        owner: context.repo.owner,
        repo: context.repo.repo,
        job_id: job.id,
      });
      logs = typeof data === 'string' ? data : String(data);
    } catch (err) {
      core.warning(`Failed to download logs for ${councilName}: ${err.message}`);
      logs = `(logs unavailable: ${err.message})`;
    }

    // Truncate to last 3000 chars — the failure stack trace is at the end
    if (logs.length > 3000) {
      logs = logs.slice(-3000);
    }

    failures.push({
      councilName,
      jobId: job.id,
      jobUrl: job.html_url,
      logs,
      needsInvestigation: true,
    });
  }

  // Write failure context to file for downstream steps
  const fs = require('fs');
  const failureContext = { runId, runUrl, failures };
  fs.writeFileSync('failure-context.json', JSON.stringify(failureContext, null, 2));

  core.info(`Wrote failure-context.json with ${failures.length} failure(s)`);
  core.setOutput('has_failures', 'true');
  core.setOutput('failure_count', String(failures.length));
  core.setOutput('run_url', runUrl);
};
