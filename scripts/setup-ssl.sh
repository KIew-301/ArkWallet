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

# Replace staging config with SSL config
cat > /etc/nginx/sites-available/arkwallet.conf << 'NGINXEOF'
server {
    listen 80;
    server_name arkwallet.ru;

    location /.well-known/acme-challenge/ {
        root /var/www/certbot;
    }

    location / {
        return 301 https://$host$request_uri;
    }
}

server {
    listen 443 ssl;
    server_name arkwallet.ru;

    ssl_certificate /etc/letsencrypt/live/arkwallet.ru/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/arkwallet.ru/privkey.pem;

    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    ssl_prefer_server_ciphers on;

    client_max_body_size 10m;

    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }
}
NGINXEOF

nginx -t && systemctl reload nginx

# Setup auto-renewal
systemctl enable certbot.timer
systemctl start certbot.timer

echo ""
echo "=== SSL configured successfully ==="
echo "Site: https://$DOMAIN"
echo "Auto-renewal: certbot.timer active"
