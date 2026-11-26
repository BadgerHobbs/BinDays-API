#!/usr/bin/env bash
#
# Trigger the daily integration test run through the workflow_dispatch API.
#
# GitHub's `schedule` trigger has been starting the daily run 4-11 hours late, while
# workflow_dispatch starts straight away. This script runs from an external scheduler at 08:00 UTC;
# the workflow's own cron is only a fallback that skips itself once this run has happened.
#
# Token: a fine-grained personal access token for BadgerHobbs/BinDays-API with
# "Actions: Read and write" permission, stored in a file readable only by the user running this:
#   install -m 600 /dev/null /etc/bindays/github-dispatch-token   # then paste the token into it
#
# Example crontab entry (CRON_TZ keeps it at 08:00 UTC through BST):
#   CRON_TZ=UTC
#   0 8 * * * /opt/bindays/dispatch-daily-run.sh >> /var/log/bindays-dispatch.log 2>&1

set -euo pipefail

REPO="${BINDAYS_REPO:-BadgerHobbs/BinDays-API}"
WORKFLOW="${BINDAYS_WORKFLOW:-integration-tests.yml}"
REF="${BINDAYS_REF:-main}"
TOKEN_FILE="${BINDAYS_DISPATCH_TOKEN_FILE:-/etc/bindays/github-dispatch-token}"

if [[ ! -r "$TOKEN_FILE" ]]; then
	echo "$(date -u +%FT%TZ) Token file $TOKEN_FILE is missing or unreadable." >&2
	exit 1
fi

TOKEN="$(tr -d '[:space:]' < "$TOKEN_FILE")"

# The API returns 204 No Content on success. Retry a few times in case of a transient network error.
for attempt in 1 2 3; do
	STATUS="$(curl -sS -o /tmp/bindays-dispatch-response -w '%{http_code}' -X POST \
		-H "Accept: application/vnd.github+json" \
		-H "Authorization: Bearer $TOKEN" \
		-H "X-GitHub-Api-Version: 2022-11-28" \
		"https://api.github.com/repos/$REPO/actions/workflows/$WORKFLOW/dispatches" \
		-d "{\"ref\":\"$REF\"}" || echo "000")"

	if [[ "$STATUS" == "204" ]]; then
		echo "$(date -u +%FT%TZ) Dispatched $WORKFLOW on $REF."
		exit 0
	fi

	echo "$(date -u +%FT%TZ) Attempt $attempt failed with HTTP $STATUS: $(cat /tmp/bindays-dispatch-response 2>/dev/null)" >&2
	sleep $((attempt * 30))
done

exit 1
