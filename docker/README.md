# Docker

Минимальный стек: SQL Server, одноразовая инициализация БД, MailHog, веб-приложение (MVC + static).

## Запуск после clone

Из **корня репозитория** (файлы `.bak` не нужны):

```bash
docker compose up --build
```

Конфигурация по умолчанию — [.env.docker.example](../.env.docker.example) (уже подключён в compose). Локальные переопределения: скопируйте в `.env.docker` (файл в git не попадает).

| URL | Назначение |
|-----|------------|
| http://localhost:8080 | Приложение |
| http://localhost:8025 | MailHog UI |
| localhost:1433 | SQL Server (отладка) |

Учётная запись dev: `admin@local.test` / `Admin123!` (см. `DOCKER_BOOTSTRAP_*` в `.env.docker.example`).

## Что происходит при первом старте

1. **mssql** — поднимается SQL Server 2022.
2. **db-init** — если в `docker/backups/` **нет** пары `.bak`, выполняются SQL из `docker/mssql/sql/` (схема очереди + демо-талоны). Если оба `.bak` есть — `RESTORE` обеих баз.
3. **web** — EF-миграции **UserDb**, seed ролей/permissions, опционально bootstrap admin.

Повторный `docker compose up` не перезаписывает существующие БД (данные в volume `mssql-data`).

## Свои данные из backup

1. Положите `UserDb.bak` и `ElectronicQueueProf.bak` в [docker/backups/](backups/) — см. [backups/README.md](backups/README.md).
2. Сброс volume и пересоздание:

   ```bash
   docker compose down -v
   docker compose up --build
   ```

## Сброс к демо-данным

```bash
docker compose down -v
docker compose up --build
```

## Внешний SQL Server

1. Удалите или закомментируйте сервисы `mssql` и `db-init` в `docker-compose.yml`.
2. У `web` уберите зависимость `db-init`.
3. В `.env.docker` укажите connection strings, например `Server=host.docker.internal,1433;...` (Windows Docker Desktop).

## Устранение неполадок

| Симптом | Что проверить |
|--------|----------------|
| `db-init` exit 1, один `.bak` | Нужны **оба** файла или **ни одного** |
| `web` не стартует | Логи: JWT, connection strings; пароль SA совпадает везде |
| Дашборд пустой | БД `ElectronicQueueProf`; для SQL bootstrap — талоны на сегодня (`DEMO-*`) |
| `/health` unhealthy | Логи контейнера `web` после migrate |

Для production смените `MSSQL_SA_PASSWORD`, `AppSettings__Token`, отключите `DOCKER_BOOTSTRAP_ADMIN=false`.
