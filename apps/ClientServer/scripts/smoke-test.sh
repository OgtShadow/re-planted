#!/usr/bin/env bash
# Post-deployment smoke test for ClientServer running on a Raspberry Pi (or any Linux host).
#
# Verifies:
#   1. ClientServer itself is up (health endpoint).
#   2. ClientServer can reach the main Server over HTTP (server-check endpoint).
#   3. The IoT controller status/topology/telemetry endpoints respond for the configured client.
#   4. The local MQTT broker port is reachable.
#
# Usage:
#   ./smoke-test.sh [clientServerBaseUrl] [clientId] [mqttHost] [mqttPort]
#
# Defaults match docker-compose.raspberrypi.yml.
set -uo pipefail

CLIENT_SERVER_URL="${1:-http://localhost:8082}"
CLIENT_ID="${2:-1}"
MQTT_HOST="${3:-localhost}"
MQTT_PORT="${4:-1883}"

PASS=0
FAIL=0

check() {
    local description="$1"
    local url="$2"
    local expected_status="${3:-200}"

    local status
    status=$(curl -s -o /tmp/smoke-test-body.json -w "%{http_code}" --max-time 10 "$url")

    if [ "$status" = "$expected_status" ]; then
        echo "[PASS] $description -> HTTP $status"
        PASS=$((PASS + 1))
    else
        echo "[FAIL] $description -> HTTP $status (expected $expected_status)"
        echo "       body: $(cat /tmp/smoke-test-body.json 2>/dev/null | head -c 300)"
        FAIL=$((FAIL + 1))
    fi
}

echo "== ClientServer smoke test =="
echo "Target: $CLIENT_SERVER_URL (clientId=$CLIENT_ID)"
echo

check "ClientServer health" "$CLIENT_SERVER_URL/api/client-server/health"
check "Connectivity to main Server" "$CLIENT_SERVER_URL/api/client-server/server-check"
check "Controller status" "$CLIENT_SERVER_URL/api/client-server/controllers/$CLIENT_ID/status"
check "Controller topology" "$CLIENT_SERVER_URL/api/client-server/controllers/$CLIENT_ID/topology"
check "Controller telemetry (current)" "$CLIENT_SERVER_URL/api/client-server/controllers/$CLIENT_ID/telemetry/current"
check "Controller configuration snapshot" "$CLIENT_SERVER_URL/api/client-server/controllers/$CLIENT_ID/configuration"

echo
echo "== MQTT broker reachability =="
if command -v nc >/dev/null 2>&1; then
    if nc -z -w 5 "$MQTT_HOST" "$MQTT_PORT"; then
        echo "[PASS] MQTT broker reachable at $MQTT_HOST:$MQTT_PORT"
        PASS=$((PASS + 1))
    else
        echo "[FAIL] MQTT broker NOT reachable at $MQTT_HOST:$MQTT_PORT"
        FAIL=$((FAIL + 1))
    fi
else
    echo "[SKIP] 'nc' not installed, cannot check MQTT port directly. Install with: sudo apt-get install netcat-openbsd"
fi

echo
echo "== Summary =="
echo "Passed: $PASS, Failed: $FAIL"

rm -f /tmp/smoke-test-body.json

if [ "$FAIL" -gt 0 ]; then
    exit 1
fi
exit 0
