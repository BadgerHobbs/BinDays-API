/**
 * Create a summary when no eligible issues are found.
 *
 * @param {Object} github - GitHub API client
 * @param {Object} context - GitHub Actions context
 * @param {Object} core - GitHub Actions core utilities
 */
module.exports = async ({ github, context, core }) => {
  await core.summary
    .addHeading('Scheduled Collector Implementation')
    .addRaw('No eligible New Collector issues found.')
    .write();
};
