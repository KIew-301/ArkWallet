#!/bin/bash
set -e

DOMAIN="arkwallet.ru"
APP_DIR="${1:-/opt/arkwallet}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "=== Ensuring nginx setup for $DOMAIN ==="

# 1. Install nginx + certbot if missing
if ! command -v nginx &> /dev/null; then
  echo "[1/5] Installing nginx and certbot..."
  apt-get update -qq && apt-get install -y -qq nginx certbot python3-certbot-nginx
  systemctl enable nginx
  systemctl start nginx
else
  echo "[1/5] nginx already installed"
fi

# 2. Remove default site if active
if [ -L /etc/nginx/sites-enabled/default ] || [ -f /etc/nginx/sites-enabled/default ]; then
  echo "[2/5] Removing default nginx site..."
  rm -f /etc/nginx/sites-enabled/default
else
  echo "[2/5] Default site already removed"
fi

# 3. Sync nginx config from repo
echo "[3/5] Syncing nginx config..."
NGINX_SRC="$APP_DIR/scripts/nginx"
NGINX_DST="/etc/nginx/sites-available"
NGINX_CHANGED=false

if [ -f "$NGINX_SRC/arkwallet.conf" ]; then
  if ! diff -q "$NGINX_SRC/arkwallet.conf" "$NGINX_DST/arkwallet.conf" > /dev/null 2>&1; then
    cp "$NGINX_SRC/arkwallet.conf" "$NGINX_DST/arkwallet.conf"
    NGINX_CHANGED=true
    echo "  arkwallet.conf updated"
  else
    echo "  arkwallet.conf unchanged"
  fi
fi

if [ -f "$NGINX_SRC/arkwallet-staging.conf" ]; then
  if ! diff -q "$NGINX_SRC/arkwallet-staging.conf" "$NGINX_DST/arkwallet-staging.conf" > /dev/null 2>&1; then
    cp "$NGINX_SRC/arkwallet-staging.conf" "$NGINX_DST/arkwallet-staging.conf"
    echo "  arkwallet-staging.conf updated"
  fi
fi

ln -sf "$NGINX_DST/arkwallet.conf" /etc/nginx/sites-enabled/arkwallet.conf

# 4. Obtain SSL certificate if missing
echo "[4/5] Checking SSL certificate..."
if [ -f "/etc/letsencrypt/live/$DOMAIN/fullchain.pem" ]; then
  echo "  SSL certificate exists"
  # Check if cert expires within 30 days, renew if so
  EXPIRY=$(openssl x509 -enddate -noout -in "/etc/letsencrypt/live/$DOMAIN/fullchain.pem" | cut -d= -f2)
  EXPIRY_EPOCH=$(date -d "$EXPIRY" +%s 2>/dev/null || date -j -f "%b %d %H:%M:%S %Y %Z" "$EXPIRY" +%s 2>/dev/null || echo 0)
  NOW_EPOCH=$(date +%s)
  DAYS_LEFT=$(( (EXPIRY_EPOCH - NOW_EPOCH) / 86400 ))
  if [ "$DAYS_LEFT" -lt 30 ] 2>/dev/null; then
    echo "  Certificate expires in $DAYS_LEFT days, renewing..."
    certbot renew --non-quiet 2>/dev/null || true
    NGINX_CHANGED=true
  fi
else
  echo "  No SSL certificate found, obtaining..."
  certbot certonly --nginx -d "$DOMAIN" --non-interactive --agree-tos --email "admin@$DOMAIN" || \
  certbot certonly --webroot -w /var/www/certbot -d "$DOMAIN" --non-interactive --agree-tos --email "admin@$DOMAIN" || {
    echo "  [!] Failed to obtain certificate. Using staging config (HTTP only)."
    ln -sf "$NGINX_DST/arkwallet-staging.conf" /etc/nginx/sites-enabled/arkwallet.conf
  }
  NGINX_CHANGED=true
fi

# 5. Ensure certbot auto-renewal
systemctl enable certbot.timer 2>/dev/null || true
systemctl start certbot.timer 2>/dev/null || true

# 6. Create htpasswd for monitoring endpoints
echo "[5/5] Setting up htpasswd..."
ENV_FILE="$APP_DIR/.env"
if [ -f "$ENV_FILE" ] && command -v openssl &> /dev/null; then
  METRICS_KEY=$(grep "^METRICS_API_KEY=" "$ENV_FILE" | cut -d'=' -f2-)
  if [ -n "$METRICS_KEY" ]; then
    HTPASSWD="/etc/nginx/.htpasswd"
    if [ ! -f "$HTPASSWD" ] || ! grep -q "^admin:" "$HTPASSWD" 2>/dev/null; then
      echo -n "admin:" > "$HTPASSWD"
      openssl passwd -apr1 -stdin <<< "$METRICS_KEY" >> "$HTPASSWD"
      echo "  htpasswd created"
      NGINX_CHANGED=true
    fi
  fi
fi

# Reload nginx if anything changed
if nginx -t 2>&1; then
  if [ "$NGINX_CHANGED" = true ]; then
    systemctl reload nginx
    echo "  nginx reloaded"
  else
    echo "  nginx config unchanged, no reload needed"
  fi
else
  echo "  [!] nginx config test failed!"
  exit 1
fi

echo ""
echo "=== nginx setup complete ==="
echo "  HTTP:  http://$DOMAIN (redirects to HTTPS)"
echo "  HTTPS: https://$DOMAIN"
