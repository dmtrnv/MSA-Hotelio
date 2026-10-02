#!/usr/bin/env bash
set -euo pipefail

GATEWAY_NS="${GATEWAY_NS:-istio-system}"
GATEWAY_SVC="${GATEWAY_SVC:-istio-ingressgateway}"
LOCAL_PORT="${LOCAL_PORT:-9090}"
REQUESTS="${REQUESTS:-200}"
EXPECTED_V2_PERCENT="${EXPECTED_V2_PERCENT:-10}"
TOLERANCE_PERCENT="${TOLERANCE_PERCENT:-5}"

PF_PID=""
cleanup() {
  if [ -n "${PF_PID}" ]; then
    kill "${PF_PID}" >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

start_port_forward() {
  echo "▶️ Поднимаю port-forward: localhost:${LOCAL_PORT} -> ${GATEWAY_SVC}:80..."
  kubectl -n "${GATEWAY_NS}" port-forward "svc/${GATEWAY_SVC}" "${LOCAL_PORT}:80" >/dev/null 2>&1 &
  PF_PID=$!
  for _ in $(seq 1 40); do
    if curl -s -o /dev/null "http://localhost:${LOCAL_PORT}/ping"; then
      return 0
    fi
    sleep 0.5
  done
  echo "❌ Не удалось дождаться Istio Ingress Gateway через port-forward"
  exit 1
}

echo "▶️ Проверяем canary release (~${EXPECTED_V2_PERCENT}% v2, rest v1)..."
start_port_forward

v1=0
v2=0
errors=0
for _ in $(seq 1 "${REQUESTS}"); do
  resp=$(curl -s "http://localhost:${LOCAL_PORT}/ping" || true)
  case "${resp}" in
    *"v2"*) v2=$((v2 + 1)) ;;
    *"pong"*) v1=$((v1 + 1)) ;;
    *) errors=$((errors + 1)) ;;
  esac
done

echo "Результаты: v1=${v1}, v2=${v2}, ошибок=${errors} (всего запросов: ${REQUESTS})"

if [ "${errors}" -ne 0 ]; then
  echo "❌ Получены ответы, не являющиеся 'pong'/'pong v2'"
  exit 1
fi

SHARE=$(awk -v v2="${v2}" -v n="${REQUESTS}" 'BEGIN { printf "%.1f", v2 * 100 / n }')
echo "Доля v2: ${SHARE}% (ожидается ~${EXPECTED_V2_PERCENT}% ± ${TOLERANCE_PERCENT}%)"

if ! awk -v share="${SHARE}" -v expected="${EXPECTED_V2_PERCENT}" -v tol="${TOLERANCE_PERCENT}" \
  'BEGIN { exit (share < expected - tol || share > expected + tol) ? 1 : 0 }'; then
  echo "❌ Разделение трафика вне допуска"
  exit 1
fi

echo
echo "✅ Canary release работает: ~90% v1 / ~10% v2"
