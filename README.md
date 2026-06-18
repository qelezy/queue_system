# WebApplication

Мониторинг электронной очереди и отчёты (ASP.NET Core 10 MVC).

## Docker

Требования: [Docker Desktop](https://www.docker.com/products/docker-desktop/) с Docker Compose v2.

1. Положите `UserDb.bak` и `ElectronicQueueProf.bak` в [docker/backups/](docker/backups/).
2. При необходимости скопируйте [.env.docker.example](.env.docker.example) в `.env.docker`.
3. Из корня репозитория:

```bash
docker compose up --build
```

| Сервис | URL |
|--------|-----|
| Приложение | http://localhost:8080 |
| MailHog | http://localhost:8025 |
| SQL Server | localhost:1433 |

Вход — учётные записи из `UserDb.bak`. При первом старте mssql восстанавливает БД из `.bak` (1–2 мин). Повторный `docker compose up` не перезаписывает данные.

Сброс данных:

```bash
docker compose down -v
docker compose up --build
```

Если restore падает с Msg 3169 — backup создан на SQL новее 2022, пересоздайте `.bak` на SQL ≤ 2022. Если `web` падает на EF-миграциях — `DOCKER_SKIP_EF_MIGRATE=true` в `.env.docker`.

## Локальная разработка без Docker

См. [WebApplication/.env.example](WebApplication/.env.example) — SQL Server на хосте, `dotnet run` в каталоге `WebApplication/`.
