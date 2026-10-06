#!/usr/bin/env bash
# Browser test of single sign-on against the container with the test identity provider
# (plan docs/plans/identity-provider-vorlagen.md E29). Builds both images, starts them, waits
# for /health, runs the Playwright project "sso" and always cleans up with "down -v".
#
# Usage (repo root): scripts/sso-e2e.sh
# Requires: Docker with compose, Node 22 + pnpm, Chromium for Playwright
#           (pnpm exec playwright install --with-deps chromium in src/ReadyStackGo.WebUi).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE=(docker compose -f "$ROOT/docker-compose.yml" -f "$ROOT/docker-compose.sso-e2e.yml")

cleanup() {
  "${COMPOSE[@]}" logs --no-color > "$ROOT/sso-e2e-containers.log" 2>&1 || true
  "${COMPOSE[@]}" down -v --remove-orphans || true
}
trap cleanup EXIT

"${COMPOSE[@]}" down -v --remove-orphans || true
"${COMPOSE[@]}" build
"${COMPOSE[@]}" up -d readystackgo test-idp

wait_for() {
  local url="$1"
  for _ in $(seq 1 90); do
    if curl -fs "$url" > /dev/null; then
      return 0
    fi
    sleep 2
  done
  echo "Timed out waiting for $url" >&2
  return 1
}

wait_for http://localhost:9090/health
wait_for http://localhost:8080/health

cd "$ROOT/src/ReadyStackGo.WebUi"
pnpm run test:e2e:sso
