# WebApplication

Мониторинг электронной очереди и отчёты (ASP.NET Core 10 MVC).

## Быстрый старт (Docker)

Требования: [Docker Desktop](https://www.docker.com/products/docker-desktop/) с Docker Compose v2.

1. Положите `UserDb.bak` и `ElectronicQueueProf.bak` в [docker/backups/](docker/backups/).
2. Из корня репозитория:

```bash
docker compose up --build
```

После старта:

| Сервис | URL |
|--------|-----|
| Приложение | http://localhost:8080 |
| MailHog (письма) | http://localhost:8025 |

Вход — учётные записи из `UserDb.bak` (см. [docker/README.md](docker/README.md)).

## Локальная разработка без Docker

См. [WebApplication/.env.example](WebApplication/.env.example) — SQL Server на хосте, `dotnet run` в каталоге `WebApplication/`.
