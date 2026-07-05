#!/usr/bin/env bash
#
# Set PG_USER and PG_PASS once at the GitHub organization level.
# All deploy workflows read secrets.PG_USER / secrets.PG_PASS from here unless
# a repo or environment secret overrides them (avoid overrides).
#
# Prerequisites:
#   gh auth refresh -h github.com -s admin:org
#
# Usage:
#   export PG_USER='your_pg_role'
#   export PG_PASS='your_pg_password'
#   ./scripts/set-org-postgres-secrets.sh
#
# Optional: restrict to deploy repos instead of all org repos:
#   REPOS=gideonogegaorg/FamilyTree,gideonogegaorg/SpotifySmartPlaylists ./scripts/set-org-postgres-secrets.sh

set -euo pipefail

ORG="${GITHUB_ORG:-gideonogegaorg}"
VISIBILITY="${SECRET_VISIBILITY:-all}"
REPOS="${REPOS:-}"

for name in PG_USER PG_PASS; do
  if [ -z "${!name:-}" ]; then
    echo "::error::Set $name in the environment before running."
    exit 1
  fi
done

set_secret() {
  local name="$1" value="$2"
  if [ -n "$REPOS" ]; then
    printf '%s' "$value" | gh secret set "$name" --org "$ORG" --visibility selected --repos "$REPOS"
  else
    printf '%s' "$value" | gh secret set "$name" --org "$ORG" --visibility "$VISIBILITY"
  fi
  echo "Set organization secret: $name"
}

set_secret PG_USER "$PG_USER"
set_secret PG_PASS "$PG_PASS"

echo "Done. Ensure org secret policies allow GitHub Environments used by deploy (prod, dev) — not only legacy 'main'."
