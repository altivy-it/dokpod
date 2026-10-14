#!/usr/bin/env bash
# E2E de UI com respostas simuladas (spec 007 T028). Tudo containerizado:
# build Angular (devops) -> nginx (borda) + mock Python -> Playwright (pytest).
# Uso: frontend/tests/e2e/mock/run-mock-e2e.sh [--no-build] [--keep] [-- <args do pytest>]
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../../.." && pwd)"
ENGINE="${CONTAINER_ENGINE:-$(command -v podman || command -v docker)}"
NET=dokpod-e2e-mock
BUILD=1 KEEP=0 PYTEST_ARGS=()
while [ $# -gt 0 ]; do
  case "$1" in
    --no-build) BUILD=0 ;;
    --keep) KEEP=1 ;;
    --) shift; PYTEST_ARGS=("$@"); break ;;
  esac
  shift
done
LABEL=",z"
if [ "$BUILD" = 1 ]; then
  "$ENGINE" run --rm -v "$ROOT":/src:z -v dokpod-npm:/root/.npm -w /src/frontend/web docker.io/lzocateli/devops:cpu-ubuntu24.04 \
    bash -lc 'npm ci --no-audit --no-fund >/dev/null && npx ng build --configuration development --output-path dist/e2e >/dev/null && echo build-ok'
fi
"$ENGINE" network exists "$NET" 2>/dev/null || "$ENGINE" network create "$NET" >/dev/null
"$ENGINE" rm -f dokpod-mock dokpod-edge >/dev/null 2>&1 || true
"$ENGINE" run -d --name dokpod-mock --network "$NET" -v "$ROOT/frontend/tests/e2e/mock":/mock:ro$LABEL --entrypoint python3 \
  docker.io/lzocateli/playwright-e2e:0.1.0 /mock/control_plane_mock.py >/dev/null
"$ENGINE" run -d --name dokpod-edge --network "$NET" -v "$ROOT/frontend/web/dist/e2e/browser":/usr/share/nginx/html:ro$LABEL \
  -v "$ROOT/docs/design/app":/design:ro$LABEL \
  -v "$ROOT/frontend/tests/e2e/mock/nginx.conf":/etc/nginx/conf.d/default.conf:ro$LABEL docker.io/lzocateli/nginx:1.28.0-bookworm >/dev/null
cleanup() { [ "$KEEP" = 1 ] || "$ENGINE" rm -f dokpod-mock dokpod-edge >/dev/null 2>&1 || true; }
trap cleanup EXIT
mkdir -p "$ROOT/artifacts/e2e/mock"
"$ENGINE" run --rm --network "$NET" \
  -v "$ROOT/frontend/tests/e2e":/app/tests:ro$LABEL \
  -v "$ROOT/frontend/web/node_modules/axe-core/axe.min.js":/app/axe/axe.min.js:ro$LABEL \
  -v "$ROOT/artifacts/e2e/mock":/app/reports:z \
  -e DOKPOD_E2E_MOCK_BASE_URL=http://dokpod-edge:8080/ -e DOKPOD_E2E_AXE=/app/axe/axe.min.js \
  -w /app --entrypoint uv docker.io/lzocateli/playwright-e2e:0.1.0 \
  run --frozen pytest --confcutdir=/app/tests/mock_ui /app/tests/mock_ui -p no:cacheprovider -q --junitxml=/app/reports/junit.xml "${PYTEST_ARGS[@]}"
