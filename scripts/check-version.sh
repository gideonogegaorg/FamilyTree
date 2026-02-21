#!/usr/bin/env bash
# Validate assembly version on a deployed Family.Web (Linux).
# Usage:
#   On server:  ./scripts/check-version.sh
#   Or:         DEPLOY_PATH=/var/www/family.example.com ./scripts/check-version.sh
#   From site:  curl -s https://family.example.com/ | grep -oP 'Family Tree \K[^ -]+'

set -euo pipefail

DEPLOY_PATH="${DEPLOY_PATH:-/var/www/family.gideonogega.com}"
DLL="$DEPLOY_PATH/Family.Web.dll"

if [[ ! -f "$DLL" ]]; then
  echo "DLL not found: $DLL (set DEPLOY_PATH if needed)" >&2
  exit 1
fi

# InformationalVersion is embedded as a string (e.g. 202602123456789 or DEV-202602123456789)
echo "InformationalVersion (from DLL):"
strings "$DLL" | grep -E '^(DEV-)?[0-9]{12,}$' | head -1 || echo "(none found)"

echo ""
echo "From live site (if reachable):"
if command -v curl &>/dev/null; then
  # Use the same DEPLOY_PATH to guess a domain only for a hint; user can curl their real URL
  echo "  curl -s https://YOUR_DOMAIN/ | grep -oP 'Family Tree \\K[^ -]+'"
else
  echo "  curl -s https://YOUR_DOMAIN/ | grep -oP 'Family Tree \\K[^ -]+'"
fi
