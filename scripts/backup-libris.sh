#!/usr/bin/env bash
# Nightly backup of the Libris database and covers volume (cron, 03:30 on the BillFolder VPS).
set -euo pipefail

compose_dir="${COMPOSE_DIR:-/opt/billfolder}"
backup_dir="${BACKUP_DIR:-/opt/billfolder/backups}"
covers_volume="${COVERS_VOLUME:-billfolder_libris_covers}"
day="$(date +%F)"

cd "$compose_dir"
trap 'rm -f "$backup_dir/libris-$day.sql.gz.tmp" "$backup_dir/libris-covers-$day.tgz.tmp"' EXIT

# Written to .tmp and renamed only on success, so a failed pg_dump never leaves a file that looks like a backup.
docker compose exec -T postgres pg_dump -U billfolder libris | gzip > "$backup_dir/libris-$day.sql.gz.tmp"
mv "$backup_dir/libris-$day.sql.gz.tmp" "$backup_dir/libris-$day.sql.gz"

docker run --rm -v "$covers_volume:/c:ro" -v "$backup_dir:/b" alpine tar czf "/b/libris-covers-$day.tgz.tmp" -C /c .
mv "$backup_dir/libris-covers-$day.tgz.tmp" "$backup_dir/libris-covers-$day.tgz"

# Rotation runs only after tonight's backups exist, so failing nights never delete the last good ones.
find "$backup_dir" -name 'libris-*' -mtime +14 -delete
