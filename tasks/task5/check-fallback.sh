#!/usr/bin/env bash
set -euo pipefail

GATEWAY_NS="${GATEWAY_NS:-istio-system}"
GATEWAY_SVC="${GATEWAY_SVC:-istio-ingressgateway}"
LOCAL_PORT="${LOCAL_PORT:-9090}"
REQUESTS="${REQUESTS:-50}"
MAX_FAILURES="${MAX_FAILURES:-0}"

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

echo "▶️ Тестируем fallback route (retries + outlier detection)..."
start_port_forward

echo
echo "▶️ Текущие поды v1:"
kubectl get pods -l app=booking-service,version=v1 -o wide

V1_PODS=$(kubectl get pods -l app=booking-service,version=v1 --no-headers 2>/dev/null | wc -l | tr -d ' ')
if [ "${V1_PODS}" -lt 2 ]; then
  echo
  echo "❌ Для проверки fallback нужно минимум 2 пода v1 (replicaCount >= 2), сейчас: ${V1_PODS}."
  exit 1
fi

VICTIM=$(kubectl get pods -l app=booking-service,version=v1 -o jsonpath='{.items[0].metadata.name}')
echo
echo "▶️ Гасим один под v1: ${VICTIM}"
kubectl delete pod "${VICTIM}" --wait=false

echo "▶️ Отправляем ${REQUESTS} запросов во время отказа пода..."
ok=0
fail=0
for _ in $(seq 1 "${REQUESTS}"); do
  code=$(curl -s -o /dev/null -w '%{http_code}' "http://localhost:${LOCAL_PORT}/ping" || true)
  if [ "${code}" = "200" ]; then
    ok=$((ok + 1))
  else
    fail=$((fail + 1))
  fi
done
echo "Успешных: ${ok}, ошибок: ${fail} из ${REQUESTS}"

echo
echo "▶️ Дожидаемся восстановления подов v1..."
kubectl rollout status deployment/booking-service-v1 --timeout=120s

if [ "${fail}" -gt "${MAX_FAILURES}" ]; then
  echo
  echo "❌ Fallback/retry не отработал: ${fail} ошибок (допустимо ${MAX_FAILURES})."
  exit 1
fi

echo
echo "✅ Fallback работает: при отказе одного пода v1 запросы не теряются (retries + outlier detection)."
