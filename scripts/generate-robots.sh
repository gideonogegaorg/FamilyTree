#!/usr/bin/env bash
#
# Generates robots.txt based on IS_PRODUCTION environment variable.
# Production allows indexing, all other environments block robots.
#
# Usage:
#   IS_PRODUCTION=true ./scripts/generate-robots.sh
#

set -euo pipefail

OUTPUT="src/GMO.Family.Web/wwwroot/robots.txt"

if [ "${IS_PRODUCTION:-false}" = "true" ]; then
  cat > "$OUTPUT" <<'EOF'
User-agent: *
Allow: /
EOF
  echo "Generated $OUTPUT (production - allow indexing)"
else
  cat > "$OUTPUT" <<'EOF'
User-agent: *
Disallow: /
EOF
  echo "Generated $OUTPUT (non-production - block indexing)"
fi
