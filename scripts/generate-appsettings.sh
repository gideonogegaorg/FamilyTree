#!/usr/bin/env bash
#
# Generates appsettings.json from appsettings.json.template by replacing
# ^^PLACEHOLDER^^ tokens with environment variable values.
#
# Usage:
#   export OPENTELEMETRY_ENABLED=true SERILOG_LOG_PATH=/var/www/app/logs ...
#   ./scripts/generate-appsettings.sh [template] [output]
#
# Defaults:
#   template = src/GMO.Family.Web/appsettings.json.template
#   output   = src/GMO.Family.Web/appsettings.json

set -euo pipefail

TEMPLATE="${1:-src/GMO.Family.Web/appsettings.json.template}"
OUTPUT="${2:-src/GMO.Family.Web/appsettings.json}"

if [ ! -f "$TEMPLATE" ]; then
  echo "::error::Template not found: $TEMPLATE"
  exit 1
fi

cp "$TEMPLATE" "$OUTPUT"

replace() {
  local token="$1" value="$2"
  sed -i "s|\^\^${token}\^\^|${value}|g" "$OUTPUT"
}

replace "SERILOG_LOG_PATH"                "${SERILOG_LOG_PATH:-../../logs}"
replace "UPLOADS_PATH"                    "${UPLOADS_PATH:-../uploads}"
replace "OPENTELEMETRY_ENABLED"           "${OPENTELEMETRY_ENABLED:-false}"
replace "TELEMETRY_ENVIRONMENT_NAME"      "${TELEMETRY_ENVIRONMENT_NAME:-}"
replace "OPENTELEMETRY_OTLPEXPORTENDPOINT" "${OPENTELEMETRY_OTLPEXPORTENDPOINT:-}"
replace "OPENTELEMETRY_HEADERS"           "${OPENTELEMETRY_HEADERS:-}"
replace "OPENTELEMETRY_METRICSENDPOINT"   "${OPENTELEMETRY_METRICSENDPOINT:-}"
replace "OPENTELEMETRY_LOGGINGENDPOINT"   "${OPENTELEMETRY_LOGGINGENDPOINT:-}"
replace "GOOGLE_CLIENT_ID"               "${GOOGLE_CLIENT_ID:-}"
replace "GOOGLE_CLIENT_SECRET"           "${GOOGLE_CLIENT_SECRET:-}"

echo "Generated $OUTPUT from $TEMPLATE"
