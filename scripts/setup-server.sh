#!/bin/bash
set -e

echo "=== ArkWallet Server Setup ==="

# 1. Install Docker
if ! command -v docker &> /dev/null; then
  echo "[1/6] Installing Docker..."
  curl -fsSL --proto '=https' https://get.docker.com | sh
  systemctl enable docker
  systemctl start docker
else
  echo "[1/6] Docker already installed"
fi

# 2. Install Docker Compose plugin
if ! docker compose version &> /dev/null; then
  echo "[2/6] Installing Docker Compose plugin..."
  apt-get update && apt-get install -y docker-compose-plugin
else
  echo "[2/6] Docker Compose already installed"
fi

# 3. Create app directory
echo "[3/6] Setting up app directory..."
mkdir -p /opt/arkwallet /var/www/certbot
cd /opt/arkwallet

if [ ! -f ".git" ]; then
  git clone https://github.com/KIew-301/ArkWallet.git /opt/arkwallet
fi

# 4. Setup .env
if [ ! -f ".env" ]; then
  echo "[4/6] Creating .env from template..."
  cp .env.server .env
  echo "[!] IMPORTANT: Edit /opt/arkwallet/.env and fill in real secrets!"
else
  echo "[4/6] .env already exists"
fi

# 5. Setup nginx, SSL, htpasswd (idempotent — after repo + .env exist)
echo "[5/6] Setting up nginx..."
bash "$(dirname "$0")/ensure-nginx.sh" /opt/arkwallet

# 6. Install systemd service
echo "[6/6] Installing systemd service..."
cp scripts/arkwallet.service /etc/systemd/system/
systemctl daemon-reload
systemctl enable arkwallet.service

echo ""
echo "=== Setup Complete ==="
echo "1. Edit /opt/arkwallet/.env with real secrets"
echo "2. Run: systemctl start arkwallet"
echo "3. Check status: systemctl status arkwallet"
echo "4. Logs: docker compose -f /opt/arkwallet/docker-compose.yml logs -f"
echo "5. SSL and nginx are auto-configured by ensure-nginx.sh"
