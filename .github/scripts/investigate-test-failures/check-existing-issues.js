/**
 * Check for existing open issues to avoid duplicate investigation.
 *
 * Ensures the "collector-broken" label exists, then checks all open issues
 * with that label. Councils that already have an open issue are marked as
 * not needing investigation. Updates failure-context.json in place.
 *
 * @param {Object} github - GitHub API client
 * @param {Object} context - GitHub Actions context
 * @param {Object} core - GitHub Actions core utilities
 */
module.exports = async ({ github, context, core }) => {
  const fs = require('fs');
  const failureContext = JSON.parse(fs.readFileSync('failure-context.json', 'utf8'));

  // Ensure the "collector-broken" label exists
  try {
    await github.rest.issues.getLabel({
      owner: context.repo.owner,
      repo: context.repo.repo,
      name: 'collector-broken',
    });
    core.info('Label "collector-broken" already exists');
  } catch (err) {
    if (err.status === 404) {
      await github.rest.issues.createLabel({
        owner: context.repo.owner,
        repo: context.repo.repo,
        name: 'collector-broken',
        color: 'd73a4a',
        description: 'Collector integration test is failing',
      });
      core.info('Created "collector-broken" label');
    } else {
      throw err;
    }
  }

  // List all open issues with the "collector-broken" label
  const { data: existingIssues } = await github.rest.issues.listForRepo({
    owner: context.repo.owner,
    repo: context.repo.repo,
    labels: 'collector-broken',
    state: 'open',
    per_page: 100,
  });

  // Build a set of council names that already have open issues
  // Issue title format: "Broken collector: {CouncilName}"
  const existingCouncils = new Set(
    existingIssues
      .map(issue => {
        const match = issue.title.match(/^Broken collector:\s*(.+)$/);
        return match ? match[1].trim() : null;
      })
      .filter(Boolean)
  );

  core.info(`Found ${existingCouncils.size} council(s) with existing open issues`);

  // Mark failures that already have issues as not needing investigation
  const skipped = [];
  for (const failure of failureContext.failures) {
    if (existingCouncils.has(failure.councilName)) {
      failure.needsInvestigation = false;
      skipped.push(failure.councilName);
      core.info(`Skipping ${failure.councilName} — existing open issue`);
    }
  }

  const newFailures = failureContext.failures.filter(f => f.needsInvestigation);
  core.info(`${newFailures.length} council(s) need investigation, ${skipped.length} skipped`);

  // Write updated context back
  fs.writeFileSync('failure-context.json', JSON.stringify(failureContext, null, 2));

  core.setOutput('has_new_failures', newFailures.length > 0 ? 'true' : 'false');
  core.setOutput('skipped_councils', skipped.join(', ') || 'none');
};
