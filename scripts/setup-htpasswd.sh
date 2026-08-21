#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ENV_FILE="${1:-/opt/arkwallet/.env}"

if [ ! -f "$ENV_FILE" ]; then
  echo "Error: $ENV_FILE not found"
  exit 1
fi

METRICS_API_KEY=$(grep "^METRICS_API_KEY=" "$ENV_FILE" | cut -d'=' -f2-)
if [ -z "$METRICS_API_KEY" ]; then
  echo "Error: METRICS_API_KEY not found in $ENV_FILE"
  exit 1
fi

HTPASSWD_FILE="/etc/nginx/.htpasswd"
echo -n "admin:" > "$HTPASSWD_FILE"
openssl passwd -apr1 -stdin "$METRICS_API_KEY" >> "$HTPASSWD_FILE"

echo "htpasswd created at $HTPASSWD_FILE (user: admin)"
