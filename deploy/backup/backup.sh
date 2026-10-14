#!/bin/sh
# KADARI backups (GL-08).
#
#   backup.sh run       one backup now
#   backup.sh schedule  a backup every day at BACKUP_HOUR_UTC (the container's default)
#
# Each run:
#   1. pg_dump (custom format) of the kadari database -> vault:db/kadari-<UTC stamp>.dump
#   2. rclone sync of the MinIO bucket                  -> vault:blobs/current (overwritten/removed objects kept
#                                                          under vault:blobs/history/<stamp> for the retention window)
#   3. prune dumps and blob history older than BACKUP_RETENTION_DAYS
#   4. ping BACKUP_HEARTBEAT_URL, so a missing backup raises an alert (healthchecks.io or similar)
#
# "vault" is an rclone crypt remote over the off-site bucket: file contents and names are encrypted client-side with
# BACKUP_ENCRYPTION_PASSWORD / _SALT. Keep both in the password manager — without them a backup cannot be restored.
set -eu

# All remotes come from the environment; an empty config file keeps rclone from warning that it has none.
mkdir -p "${HOME}/.config/rclone" && touch "${HOME}/.config/rclone/rclone.conf"

configure_vault() {
    export RCLONE_CONFIG_VAULT_TYPE=crypt
    export RCLONE_CONFIG_VAULT_REMOTE="offsite:${OFFSITE_BUCKET}/kadari"
    RCLONE_CONFIG_VAULT_PASSWORD="$(rclone obscure "$BACKUP_ENCRYPTION_PASSWORD")"
    RCLONE_CONFIG_VAULT_PASSWORD2="$(rclone obscure "$BACKUP_ENCRYPTION_SALT")"
    export RCLONE_CONFIG_VAULT_PASSWORD RCLONE_CONFIG_VAULT_PASSWORD2
}

log() {
    # One JSON object per line, like the rest of the stack's logs.
    printf '{"time":"%s","service":"backup","message":"%s"}\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$1"
}

run_backup() {
    stamp="$(date -u +%Y%m%dT%H%M%SZ)"
    dump="/tmp/kadari-${stamp}.dump"

    log "backup ${stamp} started"
    pg_dump --format=custom --no-owner --file="$dump"
    rclone copyto "$dump" "vault:db/kadari-${stamp}.dump"
    rm -f "$dump"

    rclone sync minio:kadari vault:blobs/current --backup-dir "vault:blobs/history/${stamp}"

    rclone delete vault:db --min-age "${BACKUP_RETENTION_DAYS}d"
    rclone purge "vault:blobs/history" --min-age "${BACKUP_RETENTION_DAYS}d" 2>/dev/null || true

    if [ -n "${BACKUP_HEARTBEAT_URL:-}" ]; then
        wget -q -O /dev/null "$BACKUP_HEARTBEAT_URL" || log "heartbeat ping failed"
    fi

    log "backup ${stamp} finished"
}

seconds_until_next_run() {
    now="$(date -u +%s)"
    target="$(date -u -d "$(date -u +%Y-%m-%d) $(printf '%02d' "${BACKUP_HOUR_UTC:-0}"):00:00" +%s)"
    if [ "$target" -le "$now" ]; then
        target=$((target + 86400))
    fi
    echo $((target - now))
}

configure_vault

case "${1:-schedule}" in
    run)
        run_backup
        ;;
    schedule)
        log "scheduled daily at $(printf '%02d' "${BACKUP_HOUR_UTC:-0}"):00 UTC"
        while true; do
            sleep "$(seconds_until_next_run)"
            # A failed run is logged and retried tomorrow; the missing heartbeat is what alerts.
            run_backup || log "backup failed"
        done
        ;;
    *)
        echo "usage: backup.sh run|schedule" >&2
        exit 2
        ;;
esac
