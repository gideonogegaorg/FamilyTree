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
#   template = src/GMO.FamilyTree.Web/appsettings.json.template
#   output   = src/GMO.FamilyTree.Web/appsettings.json

set -euo pipefail

TEMPLATE="${1:-src/GMO.FamilyTree.Web/appsettings.json.template}"
OUTPUT="${2:-src/GMO.FamilyTree.Web/appsettings.json}"

if [ ! -f "$TEMPLATE" ]; then
  echo "::error::Template not found: $TEMPLATE"
  exit 1
fi

cp "$TEMPLATE" "$OUTPUT"

replace() {
  local token="$1" value="$2"
  # Escape for sed replacement: \ and & are special
  value="${value//\\/\\\\}"
  value="${value//&/\\&}"
  sed -i "s|\^\^${token}\^\^|${value}|g" "$OUTPUT"
}

replace_bool() {
  local token="$1" value="${2:-false}"
  if [ -z "$value" ]; then
    value="false"
  fi
  sed -i "s|\^\^${token}\^\^|${value}|g" "$OUTPUT"
}

replace "POSTGRES_CONNECTION_STRING"      "${POSTGRES_CONNECTION_STRING:-}"
replace "SERILOG_LOG_PATH"                "${SERILOG_LOG_PATH:-../../logs}"
replace "UPLOADS_PATH"                    "${UPLOADS_PATH:-../uploads}"
replace "PHOTOS_PROVIDER"                 "${PHOTOS_PROVIDER:-Local}"
replace "S3_PHOTOS_BUCKET"                "${S3_PHOTOS_BUCKET:-}"
replace "S3_SERVICE_URL"                  "${S3_SERVICE_URL:-}"
replace "S3_ACCESS_KEY"                   "${S3_ACCESS_KEY:-}"
replace "S3_SECRET_KEY"                   "${S3_SECRET_KEY:-}"
replace "S3_REGION"                       "${S3_REGION:-us-east-1}"
replace "PHOTOS_LOCAL_BASE_PATH"          "${PHOTOS_LOCAL_BASE_PATH:-uploads/photos}"

# Storage prefix: explicit PHOTOS_STORAGE_PREFIX, or PHOTOS_APP_NAME/PHOTOS_ENVIRONMENT, or PHOTOS_APP_NAME/TELEMETRY_ENVIRONMENT_NAME
if [ -z "${PHOTOS_STORAGE_PREFIX:-}" ]; then
  if [ -n "${PHOTOS_APP_NAME:-}" ] && [ -n "${PHOTOS_ENVIRONMENT:-}" ]; then
    PHOTOS_STORAGE_PREFIX="${PHOTOS_APP_NAME}/${PHOTOS_ENVIRONMENT}"
  elif [ -n "${PHOTOS_APP_NAME:-}" ] && [ -n "${TELEMETRY_ENVIRONMENT_NAME:-}" ]; then
    PHOTOS_STORAGE_PREFIX="${PHOTOS_APP_NAME}/${TELEMETRY_ENVIRONMENT_NAME}"
  fi
fi
replace "PHOTOS_STORAGE_PREFIX"           "${PHOTOS_STORAGE_PREFIX:-}"

replace "OPENTELEMETRY_OTLPEXPORTENDPOINT" "${OPENTELEMETRY_OTLPEXPORTENDPOINT:-}"
replace "OPENTELEMETRY_HEADERS"           "${OPENTELEMETRY_HEADERS:-}"
replace "OPENTELEMETRY_METRICSENDPOINT"   "${OPENTELEMETRY_METRICSENDPOINT:-}"
replace "OPENTELEMETRY_LOGGINGENDPOINT"   "${OPENTELEMETRY_LOGGINGENDPOINT:-}"
replace_bool "OPENTELEMETRY_ENABLED"      "${OPENTELEMETRY_ENABLED:-false}"
replace "TELEMETRY_ENVIRONMENT_NAME"      "${TELEMETRY_ENVIRONMENT_NAME:-}"
replace "GOOGLE_CLIENT_ID"               "${GOOGLE_CLIENT_ID:-}"
replace "GOOGLE_CLIENT_SECRET"           "${GOOGLE_CLIENT_SECRET:-}"
replace "EMAIL_PROVIDER"                 "${EMAIL_PROVIDER:-Logging}"
# Prefer explicit EMAIL_FROM_ADDRESS; otherwise noreply@{EMAIL_DOMAIN} (EMAIL_DOMAIN often = FULL_HOSTNAME).
if [ -z "${EMAIL_FROM_ADDRESS:-}" ] && [ -n "${EMAIL_DOMAIN:-}" ]; then
  EMAIL_FROM_ADDRESS="noreply@${EMAIL_DOMAIN}"
fi
replace "EMAIL_FROM_ADDRESS"             "${EMAIL_FROM_ADDRESS:-}"
replace "EMAIL_FROM_DISPLAY_NAME"        "${EMAIL_FROM_DISPLAY_NAME:-GOOM Family Tree}"
replace "EMAIL_REPLY_TO_ADDRESS"         "${EMAIL_REPLY_TO_ADDRESS:-}"
replace "EMAIL_REGION"                   "${EMAIL_REGION:-us-east-1}"
replace_bool "IS_PRODUCTION"              "${IS_PRODUCTION:-false}"

echo "Generated $OUTPUT from $TEMPLATE"
