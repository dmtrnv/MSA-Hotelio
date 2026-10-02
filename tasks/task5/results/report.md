Далее описаны изменения, сделанные в рамках решения задачи.

1. Доработан код сервиса booking-service:
   - /ping читает переменную PING_VERSION и добавляет её к ответу (pong для v1 без переменной, pong v2 для v2)
   - включение роута /feature происходит:
     - если задана переменная PING_VERSION и в запросе присутствует хедера X-Feature-Enabled или задана переменная ENABLE_FEATURE_X (v2)
     - если не задана переменная PING_VERSION и задана переменная ENABLE_FEATURE_X (v1)
2. Установлен istio в minikube и включена автоматическая инъекция istio в default namespace
3. Добавлены istio-манифесты: gateway, virtual-service, destination-rule, envoy-filter
4. Доработан helm chart, чтобы istio мог использовать subset'ы (v1/v2) и общий service
5. Собирается один образ, который загружается в minikube под двумя тегами: booking-service:v1 и
   booking-service:v2 (код одинаковый, поведение версии задаётся переменной PING_VERSION)
6. Разворачиваются два helm release: booking-service-v1 и booking-service-v2 из одного чарта, каждый со своим values-файлом
7. Доработан gitlab ci:
   - добавлена загрузка двух образов с разными тегами
   - добавлен шаг istio, который применяет добавленные ранее манифесты
8. Доработаны скрипты проверки check-*.sh