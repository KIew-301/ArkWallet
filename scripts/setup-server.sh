#!/bin/bash
set -e

echo "=== ArkWallet Server Setup ==="

# 1. Install Docker
if ! command -v docker &> /dev/null; then
  echo "[1/5] Installing Docker..."
  curl -fsSL https://get.docker.com | sh
  systemctl enable docker
  systemctl start docker
else
  echo "[1/5] Docker already installed"
fi

# 2. Install Docker Compose plugin
if ! docker compose version &> /dev/null; then
  echo "[2/5] Installing Docker Compose plugin..."
  apt-get update && apt-get install -y docker-compose-plugin
else
  echo "[2/5] Docker Compose already installed"
fi

# 3. Create app directory
echo "[3/5] Setting up app directory..."
mkdir -p /opt/arkwallet
cd /opt/arkwallet

if [ ! -f ".git" ]; then
  git clone https://github.com/KIew-301/ArkWallet.git /opt/arkwallet
fi

# 4. Setup .env
if [ ! -f ".env" ]; then
  echo "[4/5] Creating .env from template..."
  cp .env.server .env
  echo "[!] IMPORTANT: Edit /opt/arkwallet/.env and fill in real secrets!"
else
  echo "[4/5] .env already exists"
fi

# 5. Install systemd service
echo "[5/5] Installing systemd service..."
cp scripts/arkwallet.service /etc/systemd/system/
systemctl daemon-reload
systemctl enable arkwallet.service

echo ""
echo "=== Setup Complete ==="
echo "1. Edit /opt/arkwallet/.env with real secrets"
echo "2. Run: systemctl start arkwallet"
echo "3. Check status: systemctl status arkwallet"
echo "4. Logs: docker compose -f /opt/arkwallet/docker-compose.yml logs -f"
