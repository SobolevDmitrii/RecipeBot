#!/bin/sh
# Run from the extracted bundle: sudo sh install.sh
set -eu
cd "$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
if [ "$(id -u)" != 0 ]; then
  echo 'Run with sudo sh install.sh' >&2
  exit 1
fi
if docker compose version >/dev/null 2>&1; then
  compose() { docker compose -p recipebot-synology -f compose.yml "$@"; }
elif command -v docker-compose >/dev/null 2>&1; then
  compose() { docker-compose -p recipebot-synology -f compose.yml "$@"; }
else
  echo 'Docker Compose is unavailable. Start Container Manager first.' >&2
  exit 1
fi
for required in .env compose.yml images.tar database.dump SHA256SUMS; do
  [ -f "$required" ] || { echo "Missing: $required" >&2; exit 1; }
done
mkdir .install-lock 2>/dev/null || { echo 'Another installation may be running (.install-lock exists).' >&2; exit 1; }
trap 'rmdir .install-lock' EXIT
chmod 600 .env database.dump
sha256sum -c SHA256SUMS
compose config --quiet
docker load -i images.tar
compose up -d --no-build --pull never postgres redis

wait_healthy() {
  service="$1"
  container="$(compose ps -q "$service")"
  attempts=0
  while [ "$attempts" -lt 60 ]; do
    status="$(docker inspect --format '{{.State.Health.Status}}' "$container")"
    [ "$status" = healthy ] && return 0
    attempts=$((attempts + 1))
    sleep 2
  done
  compose logs --tail 40 "$service"
  echo "$service did not become healthy." >&2
  return 1
}
wait_healthy postgres
wait_healthy redis

if [ ! -f .database-restored ]; then
  table_count="$(compose exec -T postgres psql -U myuser -d myrecipedb -Atc "SELECT count(*) FROM information_schema.tables WHERE table_schema='public';")"
  if [ "$table_count" != 0 ]; then
    echo 'Target database is not empty. Stopping without overwriting it.' >&2
    exit 1
  fi
  redis_count="$(compose exec -T redis redis-cli DBSIZE | tr -d '\r')"
  if [ "$redis_count" != 0 ]; then
    echo 'Target Redis is not empty. Stopping without deleting its data.' >&2
    exit 1
  fi
  compose exec -T postgres pg_restore -U myuser -d myrecipedb --no-owner --no-privileges --single-transaction --exit-on-error < database.dump
  touch .database-restored
fi

compose run --rm --no-deps migrate
# The migration has just completed above, so do not run the dependency again.
compose up -d --no-build --pull never --no-deps bot
compose ps -a
echo 'Database restored; Redis started empty on first installation; bot started.'
echo 'Check logs: sudo docker compose -p recipebot-synology logs --tail 60 bot'
echo 'Then send /start to your Telegram bot.'
