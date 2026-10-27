#!/usr/bin/env bash
# Runs on the VM (uploaded by deploy-oracle.ps1): unpack the source, start PostgreSQL, import the old database once, start the app.
# Usage: remote-setup.sh build|load
set -euo pipefail
cd /opt/sat
MODE="${1:-build}"
set -a; . ./.env; set +a
chmod 600 .env

echo "==> source"
rm -rf src && mkdir src && tar -xzf src.tgz -C src

if [ "$MODE" = "load" ] && [ -f image.tar.gz ]; then
  echo "==> docker load (prebuilt image)"
  gunzip -c image.tar.gz | docker load
  rm -f image.tar.gz
fi

echo "==> postgres"
docker compose up -d postgres
for i in $(seq 1 60); do
  docker compose exec -T postgres pg_isready -U sat -d satplatform >/dev/null 2>&1 && break
  sleep 2
done

TABLES=$(docker compose exec -T postgres psql -U sat -d satplatform -tAc "select count(*) from information_schema.tables where table_schema='public'")
if [ "${MIGRATE_FROM_URL:-}" != "" ] && [ "${TABLES:-0}" = "0" ]; then
  echo "==> importing the existing database (one time)"
  NET=$(docker inspect satplatform-postgres -f '{{range $k,$v := .NetworkSettings.Networks}}{{$k}}{{end}}')
  docker run --rm --network "$NET" -e SRC="$MIGRATE_FROM_URL" -e DST="postgres://sat:${POSTGRES_PASSWORD}@postgres:5432/satplatform" postgres:17-alpine \
    sh -c 'pg_dump "$SRC" --no-owner --no-acl --no-comments | grep -v "^SET transaction_timeout" | psql "$DST" -q -v ON_ERROR_STOP=0' 2>&1 | grep -v "^NOTICE" || true
  echo "imported tables: $(docker compose exec -T postgres psql -U sat -d satplatform -tAc "select count(*) from information_schema.tables where table_schema='public'")"
else
  echo "database already has ${TABLES:-0} tables; no import"
fi

echo "==> app + caddy"
if [ "$MODE" = "load" ]; then
  docker compose up -d --no-build
else
  docker compose build --pull app
  docker compose up -d
fi
docker image prune -f >/dev/null 2>&1 || true

for i in $(seq 1 60); do
  if docker compose exec -T app curl -fsS http://localhost:8080/healthz >/dev/null 2>&1; then
    echo "app is up"; exit 0
  fi
  sleep 5
done
echo "app did not answer on /healthz; last log lines:"
docker compose logs --tail=60 app
exit 1
