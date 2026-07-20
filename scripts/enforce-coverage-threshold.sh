#!/usr/bin/env bash
# Fail if merged Cobertura line coverage is below the required threshold.
# Usage: enforce-coverage-threshold.sh <cobertura.xml> [min_line_rate_percent]

set -euo pipefail

COBERTURA_FILE="${1:?Cobertura XML path required}"
MIN_PERCENT="${2:-80}"

if [[ ! -f "$COBERTURA_FILE" ]]; then
  echo "::error::Coverage file not found: $COBERTURA_FILE" >&2
  exit 1
fi

LINE_RATE=$(grep -oP '(?<=<coverage line-rate=")[0-9.]+' "$COBERTURA_FILE" | head -1)
if [[ -z "$LINE_RATE" ]]; then
  echo "::error::Could not parse line-rate from $COBERTURA_FILE" >&2
  exit 1
fi

LINE_PERCENT=$(awk -v rate="$LINE_RATE" 'BEGIN { printf "%.2f", rate * 100 }')
MIN_RATE=$(awk -v pct="$MIN_PERCENT" 'BEGIN { printf "%.4f", pct / 100 }')

echo "Combined line coverage: ${LINE_PERCENT}% (required: ${MIN_PERCENT}%)"

if awk -v actual="$LINE_RATE" -v min="$MIN_RATE" 'BEGIN { exit (actual + 0 >= min + 0) ? 0 : 1 }'; then
  echo "Coverage threshold met."
  exit 0
fi

echo "::error::Line coverage ${LINE_PERCENT}% is below required ${MIN_PERCENT}%" >&2
exit 1
