# Secrets audit (2026-07-05)

Full scan of all **121 commits** across `main`, `dev`, and topic branches found **no real credentials** in git history or tracked files. **History rewrite is not required.**

## What was checked

- Pickaxe search for PATs, AWS keys, private keys, Google OAuth secrets, and committed credential files
- Every historical revision of `appsettings.json` (always empty OAuth placeholders or logging-only before gitignore)
- GitHub Actions workflows (all sensitive values use `${{ secrets.* }}`)

## Current protections

- [`appsettings.json`](../src/GMO.FamilyTree.Web/appsettings.json) is gitignored; use [`appsettings.json.template`](../src/GMO.FamilyTree.Web/appsettings.json.template) + [`scripts/generate-appsettings.sh`](../scripts/generate-appsettings.sh)
- Trivy filesystem scan includes **secret** detection (see [`.github/workflows/trivy.yml`](../.github/workflows/trivy.yml))
- SSH deploy step runs with `debug: false` to avoid echoing environment values in CI logs

## PostgreSQL credentials (organization secrets)

**Single source of truth:** `PG_USER` and `PG_PASS` live only under **gideonogegaorg → Organization settings → Secrets and variables → Actions**.

- Workflows reference `${{ secrets.PG_USER }}` and `${{ secrets.PG_PASS }}`; GitHub resolves org → repo → environment (first match wins).
- **Do not** set `PG_USER` / `PG_PASS` on individual repos or GitHub Environments unless a database truly uses different credentials (none do today on shared EC2 Postgres).
- When rotating the password, update the org secret and re-deploy; use [`scripts/set-org-postgres-secrets.sh`](../scripts/set-org-postgres-secrets.sh).
- After the `main` → `prod` branch rename, confirm org secret **environment** policies include `prod` and `dev` (remove stale `main`-only restrictions).

## If a secret is ever committed

1. Rotate the credential immediately (GitHub, Google OAuth, AWS, DB, etc.)
2. Remove from history with `git filter-repo` or BFG only if the secret was real — not for hostnames or dev placeholders
3. Force-push affected branches after coordinating with all collaborators
