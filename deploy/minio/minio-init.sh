#!/bin/sh
# Private bucket + an application user limited to it (GL-21). Idempotent: safe to run on every deploy.
set -eu

mc alias set local http://minio:9000 "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD" >/dev/null

mc mb --ignore-existing local/kadari
mc anonymous set none local/kadari >/dev/null

cat > /tmp/kadari-app-policy.json <<'JSON'
{
  "Version": "2012-10-17",
  "Statement": [
    { "Effect": "Allow", "Action": ["s3:GetBucketLocation", "s3:ListBucket"], "Resource": ["arn:aws:s3:::kadari"] },
    { "Effect": "Allow", "Action": ["s3:GetObject", "s3:PutObject", "s3:DeleteObject"], "Resource": ["arn:aws:s3:::kadari/*"] }
  ]
}
JSON

mc admin policy create local kadari-app /tmp/kadari-app-policy.json >/dev/null 2>&1 || true
mc admin user add local "$MINIO_APP_USER" "$MINIO_APP_PASSWORD" >/dev/null
mc admin policy attach local kadari-app --user "$MINIO_APP_USER" >/dev/null 2>&1 || true

# The Confidence Passport's own bucket, under its own policy (adding a policy never edits one already created).

echo "minio-init: bucket 'kadari' private; user '$MINIO_APP_USER' limited to it."
