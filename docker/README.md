# Docker

Минимальный стек: SQL Server, одноразовая инициализация БД, MailHog, веб-приложение (MVC + static).

## Запуск после clone

1. Положите `UserDb.bak` и `ElectronicQueueProf.bak` в [docker/backups/](backups/) — см. [backups/README.md](backups/README.md).
2. Из **корня репозитория**:

```bash
docker compose build --no-cache db-init
docker compose up --build
```

Конфигурация по умолчанию — [.env.docker.example](../.env.docker.example) (уже подключён в compose). Локальные переопределения: скопируйте в `.env.docker` (файл в git не попадает).

| URL | Назначение |
|-----|------------|
| http://localhost:8080 | Приложение |
| http://localhost:8025 | MailHog UI |
| localhost:1433 | SQL Server (отладка) |

Вход — пользователи из `UserDb.bak`. Bootstrap admin отключён (`DOCKER_BOOTSTRAP_ADMIN=false`).

## Что происходит при первом старте

1. **mssql** — поднимается SQL Server 2022.
2. **db-init** — `RESTORE` `UserDb` и `ElectronicQueueProf` из `.bak`, выравнивание compatibility level.
3. **web** — EF-миграции **UserDb** (если не `DOCKER_SKIP_EF_MIGRATE=true`), seed ролей/permissions.

Повторный `docker compose up` не перезаписывает существующие БД (данные в volume `mssql-data`).

## Сброс и повторный restore

```bash
docker compose down -v
docker compose build --no-cache db-init
docker compose up --build
```

## Внешний SQL Server

1. Удалите или закомментируйте сервисы `mssql` и `db-init` в `docker-compose.yml`.
2. У `web` уберите зависимость `db-init`.
3. В `.env.docker` укажите connection strings, например `Server=host.docker.internal,1433;...` (Windows Docker Desktop).

## Устранение неполадок

| Симптом | Что проверить |
|--------|----------------|
| `db-init` exit 1, missing backup | Оба файла в `docker/backups/` |
| `set: pipefail` / `invalid option name` | Пересоберите `db-init`: `docker compose build --no-cache db-init` |
| restore Msg 3169 | Backup новее SQL 2022 в контейнере — пересоздайте `.bak` на SQL ≤ 2022 |
| `web` не стартует на migrate | `DOCKER_SKIP_EF_MIGRATE=true` в `.env.docker` |
| `web` не стартует | JWT, connection strings; пароль SA совпадает везде |
| Дашборд пустой | Данные в `ElectronicQueueProf.bak` |
| `/health` unhealthy | Логи контейнера `web` |

Для production смените `MSSQL_SA_PASSWORD`, `AppSettings__Token`.
