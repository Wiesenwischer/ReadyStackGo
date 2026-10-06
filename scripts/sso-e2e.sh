#!/usr/bin/env bash
# Browser test of single sign-on against the container with the test identity provider
# (plan docs/plans/identity-provider-vorlagen.md E29). Builds both images, starts them, waits
# for /health, runs the Playwright project "sso" and always cleans up. The stack runs as compose
# project "rsgo-sso-e2e" with its own volumes; only those are removed, never the volumes of a
# local development stack (docker-compose.yml names them rsgo-config and rsgo-data).
#
# Usage (repo root): scripts/sso-e2e.sh
# Requires: Docker with compose, Node 22 + pnpm, Chromium for Playwright
#           (pnpm exec playwright install --with-deps chromium in src/ReadyStackGo.WebUi).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE=(docker compose -p rsgo-sso-e2e -f "$ROOT/docker-compose.yml" -f "$ROOT/docker-compose.sso-e2e.yml")
VOLUMES=(rsgo-sso-e2e-config rsgo-sso-e2e-data rsgo-sso-e2e-git-cache)
export RSGO_CONTAINER=rsgo-sso-e2e
TEMPLATES="$ROOT/src/ReadyStackGo.WebUi/e2e/test-data/identity-provider-templates"

# Without the test templates the built-in template "wysch" points at the real WYSCH.
if [ ! -f "$TEMPLATES/wysch/template.json" ] || [ ! -f "$TEMPLATES/company-sso/template.json" ]; then
  echo "Test templates missing in $TEMPLATES" >&2
  exit 1
fi

# ReadyStackGo attaches itself to the network "rsgo-net" at runtime and creates it without compose
# labels; remove it afterwards only if the test created it.
RSGO_NET_EXISTED=false
if docker network inspect rsgo-net > /dev/null 2>&1; then
  RSGO_NET_EXISTED=true
fi

reset() {
  "${COMPOSE[@]}" down --remove-orphans || true
  docker rm -f rsgo-sso-e2e rsgo-test-idp > /dev/null 2>&1 || true
  docker volume rm -f "${VOLUMES[@]}" > /dev/null || true
  # The test needs a fresh installation: stop if a volume survived (for example after a Docker crash).
  for volume in "${VOLUMES[@]}"; do
    if docker volume inspect "$volume" > /dev/null 2>&1; then
      echo "Volume $volume could not be removed" >&2
      return 1
    fi
  done
}

cleanup() {
  "${COMPOSE[@]}" logs --no-color > "$ROOT/sso-e2e-containers.log" 2>&1 || true
  reset
  if [ "$RSGO_NET_EXISTED" = false ]; then
    docker network rm rsgo-net > /dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

reset
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
