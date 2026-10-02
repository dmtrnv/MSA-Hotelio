#!/usr/bin/env bash
set -euo pipefail

GATEWAY_NS="${GATEWAY_NS:-istio-system}"
GATEWAY_SVC="${GATEWAY_SVC:-istio-ingressgateway}"
LOCAL_PORT="${LOCAL_PORT:-9090}"
REQUESTS="${REQUESTS:-30}"

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

echo "▶️ Проверка Feature Flag (X-Feature-Enabled: true)..."
start_port_forward

echo
echo "— Запросы с X-Feature-Enabled: true (ожидаем 100% v2) —"
with_flag_v2=0
for _ in $(seq 1 "${REQUESTS}"); do
  resp=$(curl -s -H "X-Feature-Enabled: true" "http://localhost:${LOCAL_PORT}/ping" || true)
  if [[ "${resp}" == *"v2"* ]]; then
    with_flag_v2=$((with_flag_v2 + 1))
  fi
done
echo "Ответов от v2: ${with_flag_v2}/${REQUESTS}"

if [ "${with_flag_v2}" -ne "${REQUESTS}" ]; then
  echo "❌ Feature flag не всегда направляет трафик на v2"
  exit 1
fi

echo
echo "— Контрольный прогон без заголовка (ожидаем ~10% v2) —"
without_flag_v2=0
without_flag_ok=0
for _ in $(seq 1 "${REQUESTS}"); do
  resp=$(curl -s "http://localhost:${LOCAL_PORT}/ping" || true)
  if [[ "${resp}" == *"pong"* ]]; then
    without_flag_ok=$((without_flag_ok + 1))
  fi
  if [[ "${resp}" == *"v2"* ]]; then
    without_flag_v2=$((without_flag_v2 + 1))
  fi
done
echo "Ответов от v2 без заголовка: ${without_flag_v2}/${REQUESTS}, валидных ответов: ${without_flag_ok}/${REQUESTS}"

if [ "${without_flag_ok}" -eq 0 ]; then
  echo "❌ Контрольный прогон без заголовка не получил ни одного ответа"
  exit 1
fi

echo
echo "✅ Feature flag через EnvoyFilter работает: с заголовком весь трафик идёт на v2."
