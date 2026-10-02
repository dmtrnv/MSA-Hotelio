Далее описаны изменения, сделанные в рамках решения задачи.

### booking-subgraph
- добавлен proto файл с контрактом booking сервиса, разработанного в task2
- добавлен bookingClient для обращения к сервису booking
- в graphql схему добавлено поле hotel с указанием, что тип является внешним
- в resolvers добавлена проверка req.headers[\'userid\'] на равенство userId, для которого запрошены бронирования
- в resolvers добавлен вызов booking сервиса, используя bookingClient, для получения бронирований пользователя

### hotel-subgraph
- добавлен hotelClient для обращения к монолиту monolith
- в resolvers добавлен вызов монолита monolith, используя hotelClient, для получения информации об отелях

### gateway
- в gateway добавлен проброс http header userid дальше по цепочке вызовов

### docker-compose.yml
- добавлена общая сеть hotelio-net для всех сервисов