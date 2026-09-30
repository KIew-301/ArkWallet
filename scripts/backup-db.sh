#!/bin/bash
set -e

# Backup the ArkWallet PostgreSQL database before the app restarts and
# pending migrations are applied on startup.
#
# Usage:
#   scripts/backup-db.sh [backup_dir]
#
# Env overrides:
#   BACKUP_CONTAINER  postgres container name (default: arkwallet-postgres)
#   BACKUP_DB         database to dump (default: $POSTGRES_DB from the container)
#   BACKUP_KEEP       how many recent backups to keep (default: 10)
#
# Exit code is non-zero on any failure (pg_dump error, empty file) so the
# deploy pipeline aborts BEFORE the app container is restarted.

CONTAINER="${BACKUP_CONTAINER:-arkwallet-postgres}"
if [ -n "$1" ]; then
  BACKUP_DIR="$1"
else
  BACKUP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/backups"
fi
KEEP="${BACKUP_KEEP:-10}"
TIMESTAMP="$(date +%Y%m%d_%H%M%S)"
FILE="${BACKUP_DIR}/arkwallet_${TIMESTAMP}.sql"

echo "=== Backing up database from container '$CONTAINER' -> $FILE ==="

mkdir -p "$BACKUP_DIR"

if ! docker exec -e PGB_DB="${BACKUP_DB:-}" "$CONTAINER" sh -c 'DB="${PGB_DB:-$POSTGRES_DB}"; PGPASSWORD="$POSTGRES_PASSWORD" pg_dump -U "$POSTGRES_USER" -d "$DB" --format=plain --no-owner --no-privileges' > "$FILE"; then
  echo "[ERROR] Backup failed. Removing partial dump."
  rm -f "$FILE"
  exit 1
fi

if [ ! -s "$FILE" ]; then
  echo "[ERROR] Backup is empty. Removing."
  rm -f "$FILE"
  exit 1
fi

echo "[OK] Backup saved: $FILE ($(du -h "$FILE" | cut -f1))"

# Retention: keep the newest KEEP backups, delete older ones.
ls -1t "${BACKUP_DIR}"/arkwallet_*.sql 2>/dev/null | tail -n +"$((KEEP + 1))" | xargs -r rm -f
echo "[OK] Keeping last $KEEP backup(s) in $BACKUP_DIR"