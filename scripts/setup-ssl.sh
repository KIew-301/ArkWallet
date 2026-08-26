#!/bin/bash
set -e

DOMAIN="arkwallet.ru"
EMAIL=""
WEBROOT="/var/www/certbot"

echo "=== Obtaining SSL certificate for $DOMAIN ==="

# Try certbot with nginx plugin (preferred)
certbot certonly --nginx -d "$DOMAIN" --non-interactive --agree-tos ${EMAIL:+--email "$EMAIL"} || \
certbot certonly --webroot -w "$WEBROOT" -d "$DOMAIN" --non-interactive --agree-tos ${EMAIL:+--email "$EMAIL"} || \
{
    echo "[!] Failed to obtain certificate. DNS may not be propagated yet."
    echo "    Make sure A-record for $DOMAIN points to this server."
    echo "    Then re-run: certbot certonly --nginx -d $DOMAIN"
    exit 1
}

# Replace staging config with SSL config from repo
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cp "$SCRIPT_DIR/nginx/arkwallet.conf" /etc/nginx/sites-available/arkwallet.conf

nginx -t && systemctl reload nginx

# Setup auto-renewal
systemctl enable certbot.timer
systemctl start certbot.timer

echo ""
echo "=== SSL configured successfully ==="
echo "Site: https://$DOMAIN"
echo "Auto-renewal: certbot.timer active"
