# Инструкция по запуску приложения

## 1. Подготовка окружения

Перед запуском убедитесь, что установлены:

- Docker
- Git

Подготовьте файлы backup (в репозиторий не входят):

- `UserDb.bak`
- `ElectronicQueueProf.bak`

## 2. Настройка базы данных

Скопируйте оба файла backup в каталог `docker/backups/`:

```
docker/backups/UserDb.bak
docker/backups/ElectronicQueueProf.bak
```

При первом запуске базы `UserDb` и `ElectronicQueueProf` будут восстановлены из этих файлов автоматически.

При необходимости скопируйте `.env.docker.example` в `.env.docker` и измените настройки (пароль SA, JWT и т.д.). По умолчанию используется `.env.docker.example`.

## 3. Запуск приложения

Из корня репозитория выполните:

```bash
docker compose up --build
```

Первый запуск может занять 1–2 минуты (восстановление backup).

После запуска:

- приложение — http://localhost:8080
- почта (MailHog) — http://localhost:8025
- SQL Server — localhost:1433

Вход выполняется учётными записями из `UserDb.bak`.

## 4. Сброс данных

Для полного сброса баз и повторного restore:

```bash
docker compose down -v
docker compose up --build
```
