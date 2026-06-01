# Тестовые талоны для мониторинга

8 талонов — **клон** реальных записей с **06.05.2026** (id 239062…239086), перенос на **сегодня по МСК** (`SYSDATETIME()`). Маршрут: **до 3 этапов** с исходного талона.

**Маркер отката:** `id_client` 99990001…99990008 (не `DASHBOARD_TEST` в `info`).

**Статусы БД:** у талона `id_status_app` 5 — «На паузе»; «Ожидание результатов» только у этапа (`id_status_item` 5). Талон 8: статус талона **1**, текущий этап **5**.

**ФИО клиента:** `info = N'-'` — в конфигураторе без ФИО пациента.

| # | Сценарий | Статус талона | Статус текущего этапа |
|---|----------|---------------|------------------------|
| 1–2 | Ожидание | 1 | 1 |
| 3–4 | Вызван | 2 | 2 |
| 5–6 | На приёме (0 мин в UI) | 3 | 3 |

**Талон 5:** 3-й этап в сиде — не плейсхолдер процедурного кабинета из эталона, а реальный врач (3-й этап эталона 239065, напр. Орловский Н.В. / Хирург). Эталоны 06.05.2026 не меняются.
| 7 | Завершён | 4 | 4 |
| 8 | Ожидание результатов | 1 (Ожидает) | 5 (Ожидание результатов) |

Номер: `Category.letter` + значение из `Numbers` (как в боевой очереди).

- **WebApplication** `/dashboard` — `date_arrival` и времена в сиде по **МСК** (как в боевой БД и `QueueDashboardClock` / `MonitoringOptions.QueueTimeZoneId`).
- **Configurator** — `Log_work.date_work` по той же локальной дате SQL-сервера (МСК).

Все `time_arrival`, открытые `time_call` / `time_start_servicing` и завершённые этапы за сегодня — **`@tNow = CAST(SYSDATETIME() AS time)`** → на `/dashboard` **0 мин** ожидания и текущего приёма (талоны 3–6 в листе и у врачей). После изменения сида обязательно **rollback → seed**.

## Полная очистка дня

Удалить **все** талоны и смены за календарную дату (не только тестовые `id_client`):

```bat
sqlcmd -S localhost\SQLEXPRESS01 -d ElectronicQueueProf -E -i purge-date.sql
```

В [`purge-date.sql`](purge-date.sql) по умолчанию `@purgeDate = '2026-05-31'`. Для текущего дня замените на `CAST(SYSDATETIME() AS date)`.

После purge при необходимости — `dashboard-test-reseed.bat`.

## Быстро

| Команда | Действие |
|---------|----------|
| `dashboard-test-remove.bat` | Удалить тестовые талоны |
| `dashboard-test-add.bat` | Удалить старые + добавить на сегодня (0 мин) |
| `dashboard-test-reseed.bat` | То же + preflight (полная диагностика) |

## Запуск

**Перед проверкой в новый день** (талоны всегда на «сегодня» МСК и время «сейчас»):

```bat
dashboard-test-reseed.bat
```

Или по шагам:

```bat
sqlcmd -S localhost\SQLEXPRESS01 -d ElectronicQueueProf -E -i dashboard-test-rollback.sql
sqlcmd -S localhost\SQLEXPRESS01 -d ElectronicQueueProf -E -i dashboard-test-preflight.sql
sqlcmd -S localhost\SQLEXPRESS01 -d ElectronicQueueProf -E -i dashboard-test-seed.sql
sqlcmd -S localhost\SQLEXPRESS01 -d ElectronicQueueProf -E -i dashboard-test-verify-today.sql
```

Та же БД, что в `.env` и Configurator_EQ2 (`localhost\SQLEXPRESS01` / `ElectronicQueueProf`).
