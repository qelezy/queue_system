# Команды: тестовые данные мониторинга

Краткая шпаргалка для добавления и удаления тестовых талонов на странице `/dashboard`.

Подробности сценариев, маркеров отката и ожидаемых метрик — в `[scripts/README-dashboard-test-seed.md](scripts/README-dashboard-test-seed.md)`.

## Подключение


| Параметр       | Значение по умолчанию    |
| -------------- | ------------------------ |
| Сервер         | `localhost\SQLEXPRESS01` |
| База           | `ElectronicQueueProf`    |
| Аутентификация | Windows (`-E`)           |


Должны совпадать с `.env` приложения и Configurator.

Все команды ниже выполняются из каталога `[scripts/](scripts/)`:

```bat
cd WebApplication\docs\archive\project-requirements\scripts
```

## Быстрые команды


| Команда                                                          | Действие                                                       |
| ---------------------------------------------------------------- | -------------------------------------------------------------- |
| `[dashboard-test-add.bat](scripts/dashboard-test-add.bat)`       | Удалить старые тестовые записи и добавить на сегодня           |
| `[dashboard-test-remove.bat](scripts/dashboard-test-remove.bat)` | Удалить только тестовые талоны (`id_client` 99990001…99990020) |
| `[dashboard-test-reseed.bat](scripts/dashboard-test-reseed.bat)` | Полный цикл: удаление + preflight + добавление + проверка      |


### Добавить тестовые записи

```bat
dashboard-test-add.bat
```

Или вручную:

```bat
sqlcmd -S localhost\SQLEXPRESS01 -d ElectronicQueueProf -E -i dashboard-test-rollback.sql
sqlcmd -S localhost\SQLEXPRESS01 -d ElectronicQueueProf -E -i dashboard-test-seed.sql
sqlcmd -S localhost\SQLEXPRESS01 -d ElectronicQueueProf -E -i dashboard-test-verify-today.sql
```

С диагностикой перед вставкой:

```bat
dashboard-test-reseed.bat
```

Или вручную:

```bat
sqlcmd -S localhost\SQLEXPRESS01 -d ElectronicQueueProf -E -i dashboard-test-rollback.sql
sqlcmd -S localhost\SQLEXPRESS01 -d ElectronicQueueProf -E -i dashboard-test-preflight.sql
sqlcmd -S localhost\SQLEXPRESS01 -d ElectronicQueueProf -E -i dashboard-test-seed.sql
sqlcmd -S localhost\SQLEXPRESS01 -d ElectronicQueueProf -E -i dashboard-test-verify-today.sql
```

### Удалить тестовые записи

```bat
dashboard-test-remove.bat
```

Или вручную:

```bat
sqlcmd -S localhost\SQLEXPRESS01 -d ElectronicQueueProf -E -i dashboard-test-rollback.sql
```

## Полная очистка дня

Удаляет **все** талоны и смены за указанную дату, не только тестовые:

```bat
sqlcmd -S localhost\SQLEXPRESS01 -d ElectronicQueueProf -E -i purge-date.sql
```

В `[purge-date.sql](scripts/purge-date.sql)` задайте `@purgeDate` — для текущего дня: `CAST(SYSDATETIME() AS date)`.

## Проверка

После добавления откройте `/dashboard` и убедитесь, что:

- лист ожидания и карточки заполнены;
- максимальное время ожидания не более 20 мин;
- клик по врачу в таблице «Состояние врачей» — модалка «Потенциальные пациенты»: талон, категория обслуживания, приоритет, время ожидания, вызов;
- в выводе `dashboard-test-verify-today.sql` нет ошибок (`category_name_check`, `call_variety_check`, `multi_patient_doctor_check` — OK).

При сбое `preflight` — см. подсказки в выводе и `[README-dashboard-test-seed.md](scripts/README-dashboard-test-seed.md)`.