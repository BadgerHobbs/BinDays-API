/**
 * Post screenshot comments on newly-created "collector broken" issues.
 *
 * Reads screenshot-urls.json (produced by upload-screenshots.sh), finds the
 * corresponding open "Broken collector: {name}" issue for each entry, and
 * posts a comment with the screenshot image.
 *
 * @param {Object} github - GitHub API client
 * @param {Object} context - GitHub Actions context
 * @param {Object} core - GitHub Actions core utilities
 */
module.exports = async ({ github, context, core }) => {
  const fs = require('fs');

  if (!fs.existsSync('screenshot-urls.json')) {
    core.info('No screenshot-urls.json — skipping');
    return;
  }

  const entries = JSON.parse(fs.readFileSync('screenshot-urls.json', 'utf8'));
  if (entries.length === 0) {
    core.info('No screenshots to attach');
    return;
  }

  const { data: issues } = await github.rest.issues.listForRepo({
    owner: context.repo.owner,
    repo: context.repo.repo,
    labels: 'collector broken',
    state: 'open',
    per_page: 100,
  });

  for (const { councilName, url } of entries) {
    const issue = issues.find(i => {
      const match = i.title.match(/^Broken collector:\s*(.+)$/);
      return match && match[1].trim() === councilName;
    });

    if (!issue) {
      core.info(`No open issue found for ${councilName} — skipping screenshot`);
      continue;
    }

    await github.rest.issues.createComment({
      owner: context.repo.owner,
      repo: context.repo.repo,
      issue_number: issue.number,
      body: `**Website screenshot:**\n\n![${councilName} website](${url})`,
    });

    core.info(`Added screenshot comment to issue #${issue.number} (${councilName})`);
  }
};
