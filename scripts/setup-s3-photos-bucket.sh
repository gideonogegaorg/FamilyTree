#!/usr/bin/env bash
#
# Creates the org-wide private S3 bucket for app photo storage (Family and others).
# Idempotent: safe to re-run.
#
# Usage:
#   ./scripts/setup-s3-photos-bucket.sh [bucket-name] [region]
#
# Defaults:
#   bucket = gideonogega-internal
#   region = us-east-1

set -euo pipefail

BUCKET="${1:-gideonogega-internal}"
REGION="${2:-us-east-1}"
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ENCRYPTION_JSON="${REPO_ROOT}/working/s3-encryption.json"

mkdir -p "${REPO_ROOT}/working"
cat > "$ENCRYPTION_JSON" <<'EOF'
{
  "Rules": [
    {
      "ApplyServerSideEncryptionByDefault": {
        "SSEAlgorithm": "AES256"
      }
    }
  ]
}
EOF

if aws s3api head-bucket --bucket "$BUCKET" 2>/dev/null; then
  echo "Bucket $BUCKET already exists."
else
  if [ "$REGION" = "us-east-1" ]; then
    aws s3api create-bucket --bucket "$BUCKET" --region "$REGION"
  else
    aws s3api create-bucket --bucket "$BUCKET" --region "$REGION" \
      --create-bucket-configuration "LocationConstraint=$REGION"
  fi
  echo "Created bucket $BUCKET in $REGION."
fi

aws s3api put-public-access-block --bucket "$BUCKET" \
  --public-access-block-configuration \
  "BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true"

aws s3api put-bucket-encryption --bucket "$BUCKET" \
  --server-side-encryption-configuration "file://${ENCRYPTION_JSON}"

aws s3api put-bucket-tagging --bucket "$BUCKET" \
  --tagging "TagSet=[{Key=org,Value=gideonogegaorg},{Key=purpose,Value=internal-private-assets}]"

echo "Bucket $BUCKET is private (public access blocked) with AES256 default encryption."
echo "Use path prefixes per app/env, e.g. family/prod/, family/dev/."
