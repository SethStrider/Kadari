#!/bin/sh
# Least-privilege roles (GL-21). Runs once, when the data volume is first initialised.
#
#   kadari_migrator  owns the database and the kadari schema; used only by the one-shot migrations job and backups
#   kadari_app       the API: read/write rows, nothing else — no DDL, no DROP, cannot touch the schema
#
# The superuser (postgres) is used by nobody at runtime.
set -eu

psql -v ON_ERROR_STOP=1 \
     -v migrator_pw="$DB_MIGRATOR_PASSWORD" \
     -v app_pw="$DB_APP_PASSWORD" \
     --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<'SQL'
CREATE ROLE kadari_migrator LOGIN PASSWORD :'migrator_pw';
CREATE ROLE kadari_app LOGIN PASSWORD :'app_pw';

ALTER DATABASE kadari OWNER TO kadari_migrator;
REVOKE ALL ON DATABASE kadari FROM PUBLIC;
GRANT CONNECT ON DATABASE kadari TO kadari_app;

-- EF's migration history lives in public; nobody but the owner may create there.
REVOKE CREATE ON SCHEMA public FROM PUBLIC;

CREATE SCHEMA IF NOT EXISTS kadari AUTHORIZATION kadari_migrator;
GRANT USAGE ON SCHEMA kadari TO kadari_app;

-- Every table and sequence the migrator creates is readable and writable by the app, and nothing more.
ALTER DEFAULT PRIVILEGES FOR ROLE kadari_migrator IN SCHEMA kadari
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO kadari_app;
ALTER DEFAULT PRIVILEGES FOR ROLE kadari_migrator IN SCHEMA kadari
    GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO kadari_app;
SQL
