/**
 * Check if the comment author has write access or is a trusted bot.
 *
 * @param {Object} github - GitHub API client
 * @param {Object} context - GitHub Actions context
 * @param {Object} core - GitHub Actions core utilities
 */
module.exports = async ({ github, context, core }) => {
  const commentUser = context.payload.comment.user;
  let hasWriteAccess = false;

  // Check if user is a bot (GitHub App)
  if (commentUser.type === 'Bot') {
    // Allow specific trusted bots
    const trustedBots = ['moley-bot[bot]'];
    hasWriteAccess = trustedBots.includes(commentUser.login);
    core.info(`Bot ${commentUser.login} - Trusted: ${hasWriteAccess}`);
  } else {
    // For regular users, check collaborator permission
    const { data: permission } = await github.rest.repos.getCollaboratorPermissionLevel({
      owner: context.repo.owner,
      repo: context.repo.repo,
      username: commentUser.login
    });
    hasWriteAccess = ['admin', 'write'].includes(permission.permission);
    core.info(`User ${commentUser.login} - Permission: ${permission.permission}`);
  }

  core.setOutput('should_run', hasWriteAccess.toString());
  if (!hasWriteAccess) {
    core.info(`User ${commentUser.login} does not have write access`);
  }
};
