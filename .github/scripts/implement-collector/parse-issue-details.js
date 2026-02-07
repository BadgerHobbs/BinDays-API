/**
 * Parse issue details and extract council name in various formats.
 *
 * @param {Object} github - GitHub API client
 * @param {Object} context - GitHub Actions context
 * @param {Object} core - GitHub Actions core utilities
 */
module.exports = async ({ github, context, core }) => {
  const issueNumber = context.payload.issue.number;
  const issueBody = context.payload.issue.body;
  const issueTitle = context.payload.issue.title;

  core.setOutput('issue_number', issueNumber);
  core.setOutput('issue_body', issueBody);

  // Use issue title as council name and convert to PascalCase
  const councilNamePascal = issueTitle
    .replace(/&/g, 'And')
    .replace(/[^a-zA-Z0-9\s]/g, '')
    .split(/\s+/)
    .map(word => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase())
    .join('');

  core.setOutput('council_name', issueTitle);
  core.setOutput('council_name_pascal', councilNamePascal);
  core.info(`Issue Number: ${issueNumber}`);
  core.info(`Council Name: ${issueTitle}`);
  core.info(`Council Name (PascalCase): ${councilNamePascal}`);
};
