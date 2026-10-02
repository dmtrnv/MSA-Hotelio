#!/usr/bin/env bash
set -euo pipefail

echo "▶️ Проверка установки Istio..."
kubectl get pods -n istio-system

echo
echo "▶️ Ожидание готовности istiod и istio-ingressgateway..."
kubectl -n istio-system wait --for=condition=Ready pod -l app=istiod --timeout=180s
kubectl -n istio-system wait --for=condition=Ready pod -l istio=ingressgateway --timeout=180s

echo
echo "▶️ Проверка автоинъекции Istio в namespace default..."
INJECTION=$(kubectl get namespace default -o jsonpath='{.metadata.labels.istio-injection}')
echo "istio-injection=${INJECTION}"
if [ "${INJECTION}" != "enabled" ]; then
  echo "❌ Автоинъекция не включена."
  exit 1
fi

echo
echo "▶️ Проверка sidecar-прокси (istio-proxy) у подов booking-service..."
kubectl get pods -l app=booking-service -o wide

echo
echo "▶️ Где размещён istio-proxy (containers / initContainers, native sidecar):"
kubectl get pods -l app=booking-service -o jsonpath='{range .items[*]}{.metadata.name}{"  containers="}{.spec.containers[*].name}{"  init="}{.spec.initContainers[*].name}{"\n"}{end}'

TOTAL=$(kubectl get pods -l app=booking-service --no-headers 2>/dev/null | wc -l | tr -d ' ')
WITH_PROXY=$(kubectl get pods -l app=booking-service -o jsonpath='{range .items[*]}{.spec.containers[*].name}{" "}{.spec.initContainers[*].name}{"\n"}{end}' | grep -c 'istio-proxy' || true)

echo
echo "Подов booking-service: ${TOTAL}, с sidecar istio-proxy: ${WITH_PROXY}"

if [ "${TOTAL}" -eq 0 ]; then
  echo "❌ Поды booking-service не найдены"
  exit 1
fi
if [ "${TOTAL}" -ne "${WITH_PROXY}" ]; then
  echo "❌ Не у всех подов есть sidecar. "
  exit 1
fi

echo
echo "✅ Istio установлен, автоинъекция включена, sidecar'ы на месте."
