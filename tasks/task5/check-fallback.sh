#!/usr/bin/env bash
set -euo pipefail

GATEWAY_NS="${GATEWAY_NS:-istio-system}"
GATEWAY_SVC="${GATEWAY_SVC:-istio-ingressgateway}"
APP_NS="${APP_NS:-default}"
LOCAL_PORT="${LOCAL_PORT:-9090}"
REQUESTS="${REQUESTS:-50}"
WARMUP="${WARMUP:-10}"
MAX_FAILURES="${MAX_FAILURES:-0}"

V1_DEPLOY="${V1_DEPLOY:-booking-service-v1}"
FALLBACK_FILTER="${FALLBACK_FILTER:-booking-v1-fallback}"

PF_PID=""
V1_SCALED_DOWN=0
V1_REPLICAS=""

cleanup() {
  if [ "${V1_SCALED_DOWN}" = "1" ]; then
    echo
    echo "▶️ Возвращаю ${V1_DEPLOY} (replicas=${V1_REPLICAS})..."
    kubectl -n "${APP_NS}" scale deployment/"${V1_DEPLOY}" --replicas="${V1_REPLICAS}" >/dev/null 2>&1 || true
    kubectl -n "${APP_NS}" rollout status deployment/"${V1_DEPLOY}" --timeout=120s || true
  fi
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

ping_once() {
  local raw
  raw=$(curl -s -w $'\n%{http_code}' "http://localhost:${LOCAL_PORT}/ping" || true)
  RESP_BODY="${raw%$'\n'*}"
  RESP_CODE="${raw##*$'\n'}"
}

wait_for_v1_gone() {
  local count
  for _ in $(seq 1 60); do
    count=$(kubectl -n "${APP_NS}" get pods -l app=booking-service,version=v1 --no-headers 2>/dev/null | wc -l | tr -d ' \r' || true)
    count="${count:-0}"
    if [ "${count}" = "0" ]; then
      return 0
    fi
    sleep 1
  done
  echo "❌ Поды v1 не исчезли за 60с"
  return 1
}

echo "▶️ Тестируем fallback v1 -> v2 (EnvoyFilter ${FALLBACK_FILTER} + Lua)..."
start_port_forward

if ! kubectl -n "${GATEWAY_NS}" get envoyfilter "${FALLBACK_FILTER}" >/dev/null 2>&1; then
  echo "⚠️ EnvoyFilter ${FALLBACK_FILTER} не найден — fallback, скорее всего, не сработает."
fi

echo
echo "▶️ Текущие поды booking-service:"
kubectl -n "${APP_NS}" get pods -l app=booking-service -o wide

V2_PODS=$(kubectl -n "${APP_NS}" get pods -l app=booking-service,version=v2 --no-headers 2>/dev/null | wc -l | tr -d ' \r' || true)
V2_PODS="${V2_PODS:-0}"
if [ "${V2_PODS}" -lt 1 ]; then
  echo
  echo "❌ Нет ни одного пода v2 — перенаправлять трафик некуда."
  exit 1
fi

V1_REPLICAS=$(kubectl -n "${APP_NS}" get deployment/"${V1_DEPLOY}" -o jsonpath='{.spec.replicas}' 2>/dev/null || true)
V1_REPLICAS="${V1_REPLICAS:-2}"

echo
echo "▶️ Убираем все endpoint'ы v1: scale ${V1_DEPLOY} 0 (было реплик: ${V1_REPLICAS})"
kubectl -n "${APP_NS}" scale deployment/"${V1_DEPLOY}" --replicas=0
V1_SCALED_DOWN=1
wait_for_v1_gone

echo "▶️ Ждём распространения конфига и прогреваем маршрут (${WARMUP} запросов, не учитываются)..."
sleep 3
for _ in $(seq 1 "${WARMUP}"); do
  ping_once || true
  sleep 0.2
done

echo
echo "▶️ Отправляем ${REQUESTS} запросов при полностью недоступном v1..."
v2=0
v1=0
fail=0
for _ in $(seq 1 "${REQUESTS}"); do
  ping_once
  if [ "${RESP_CODE}" = "200" ] && [[ "${RESP_BODY}" == *"v2"* ]]; then
    v2=$((v2 + 1))
  elif [ "${RESP_CODE}" = "200" ] && [[ "${RESP_BODY}" == *"pong"* ]]; then
    v1=$((v1 + 1))
  else
    fail=$((fail + 1))
  fi
done
echo "Ответов от v2: ${v2}, ответов от v1: ${v1}, ошибок: ${fail} из ${REQUESTS}"

if [ "${v1}" -ne 0 ]; then
  echo
  echo "❌ Получены ответы от v1, хотя все поды v1 удалены — fallback не сработал."
  exit 1
fi

if [ "${fail}" -gt "${MAX_FAILURES}" ]; then
  echo
  echo "❌ Fallback v1 -> v2 не отработал: ${fail} ошибок (допустимо ${MAX_FAILURES})."
  exit 1
fi

if [ "${v2}" -lt "${REQUESTS}" ]; then
  echo
  echo "❌ Не все запросы обслужены v2: ${v2}/${REQUESTS}."
  exit 1
fi

echo
echo "✅ Fallback работает: при полном отказе v1 все ${REQUESTS} запросов обслужены v2."
echo "▶️ ${V1_DEPLOY} будет возвращён к ${V1_REPLICAS} репликам."
