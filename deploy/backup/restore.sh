#!/bin/sh
# Restore from the encrypted off-site backups (GL-08) — the same script for a real recovery and for the drill.
#
#   restore.sh list                          list the database dumps available
#   restore.sh db <dump|latest> <database>   restore a dump into <database> (created if missing; must be empty)
#   restore.sh blobs kadari                 copy the latest blob set back into the bucket (the app user can reach no other)
#
# Drill (deploy/README.md): restore the latest dump into "kadari_restore" alongside the live database, compare row
# counts, drop it. On a fresh VPS, restore into "kadari" before the first migrations run.
set -eu

# All remotes come from the environment; an empty config file keeps rclone from warning that it has none.
mkdir -p "${HOME}/.config/rclone" && touch "${HOME}/.config/rclone/rclone.conf"

export RCLONE_CONFIG_VAULT_TYPE=crypt
export RCLONE_CONFIG_VAULT_REMOTE="offsite:${OFFSITE_BUCKET}/kadari"
RCLONE_CONFIG_VAULT_PASSWORD="$(rclone obscure "$BACKUP_ENCRYPTION_PASSWORD")"
RCLONE_CONFIG_VAULT_PASSWORD2="$(rclone obscure "$BACKUP_ENCRYPTION_SALT")"
export RCLONE_CONFIG_VAULT_PASSWORD RCLONE_CONFIG_VAULT_PASSWORD2

case "${1:-}" in
    list)
        rclone lsf vault:db | sort
        ;;
    db)
        dump="${2:?dump name or 'latest'}"
        target="${3:?target database}"
        if [ "$dump" = "latest" ]; then
            dump="$(rclone lsf vault:db | sort | tail -n 1)"
        fi
        rclone copyto "vault:db/${dump}" /tmp/restore.dump
        psql -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = '${target}'" | grep -q 1 \
            || createdb "$target"
        # Same shape as the live database: the migrator owns it, so the restored objects (created as that role)
        # land exactly as migrations would have left them, grants to kadari_app included. Needs PGUSER=postgres.
        psql -d postgres -v ON_ERROR_STOP=1 -c "ALTER DATABASE \"${target}\" OWNER TO kadari_migrator"
        pg_restore --no-owner --role=kadari_migrator --exit-on-error -d "$target" /tmp/restore.dump
        rm -f /tmp/restore.dump
        echo "restored ${dump} into ${target}"
        ;;
    blobs)
        bucket="${2:?target bucket}"
        rclone copy vault:blobs/current "minio:${bucket}"
        echo "restored blobs into ${bucket}"
        ;;
    *)
        echo "usage: restore.sh list | db <dump|latest> <database> | blobs <bucket>" >&2
        exit 2
        ;;
esac
