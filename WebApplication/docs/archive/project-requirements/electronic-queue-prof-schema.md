# Схема БД ElectronicQueueProf

Полная структура внешней БД электронной очереди для аналитики и мониторинга. Краткий перечень полей для отчётов — [electronic-queue-description.txt](electronic-queue-description.txt).

| | |
|---|---|
| **Сервер** | `.\SQLEXPRESS` |
| **База** | `ElectronicQueueProf` |
| **Схема** | `dbo` |
| **Дата снятия** | 2026-05-15 |
| **Повторное снятие** | [scripts/export-electronic-queue-schema.sql](scripts/export-electronic-queue-schema.sql) |

---

## Таблицы для системы мониторинга

Веб-приложение читает **только** эти 8 таблиц через [ElectronicQueueDbContext.cs](../Data/ElectronicQueueDbContext.cs) (режим read-only). Остальные таблицы БД относятся к UI очереди, табло, медиа и встроенной безопасности исходной системы.

### Используются сейчас (8)

| Таблица | Назначение |
|---------|------------|
| `Appointment` | Талон: дата/время прибытия, категория, завершение, пациент (`info`) |
| `List_item` | Этап маршрута: вызов, начало/конец обслуживания, врач, кабинет, статус |
| `Doctor` | Справочник врачей |
| `Cabinet` | Справочник кабинетов |
| `Category` | Категория обслуживания талона |
| `Specialty` | Специальность этапа, норматив `time_servicing` (мин) |
| `Log_work` | Фактическая смена врача в кабинете (окно работы) |
| `Status_item_list` | Статус этапа (подпись в UI/отчётах) |

### Использование по подсистемам

| Таблица | Дашборд ([QueueDashboardService.cs](../Services/QueueDashboardService.cs)) | Отчёты каталога |
|---------|-----------------------------------------------------------------------------|-----------------|
| `Appointment` | Карточки «сегодня», лист ожидания, фильтр по `date_arrival` | Все отчёты с периодом по `date_arrival`, в т.ч. service-delays |
| `List_item` | Ожидают, на приёме, обслужено, таблица очереди | Этапы, времена, маршруты, service-categories-comparison, разрывы цепочки, service-delays |
| `Doctor`, `Cabinet` | Карточки загрузки врачей, подписи в очереди | Разрезы load-and-downtime, duration; service-delays (`analysisMode`: врач или кабинет) |
| `Category` | Категория в данных талона | arrived-and-completed, service-categories-comparison |
| `Specialty` | Норматив в карточках врачей | appointment-duration, load-and-downtime; service-delays (`time_servicing`, `definition`) |
| `Log_work` | — | load-and-downtime |
| `Status_item_list` | Подпись статуса этапа в UI очереди | arrived; классификация по **полям времени** |

**Важно:** в БД у талона есть `id_status_app` → `Status_Appointment`, но мониторинг **не** читает `Status_Appointment`. Лист ожидания и метрики — только талоны с `date_arrival` за **сегодня по календарю МСК**; поля `time_*` в `List_item` — **стенные часы МСК** (склейка с `date_arrival` без перевода в UTC). Этапы с «неявным» статусом в `Status_item_list` в мониторинг не попадают.

### Не используются приложением мониторинга (23)

`Board`, `Location`, `Media`, `Numbers`, `Previous_specialty`, `Refer`, `Setting_queue`, `Shift`, `Show`, `Status_Appointment`, `Type_media`, `Version`, `Zone`, а также 12 таблиц `SEC_*` (`SEC_ACE`, `SEC_ACTION`, `SEC_LOGEVENT`, `SEC_LOGIN`, `SEC_OBJECT`, `SEC_OBJECT_TYPE`, `SEC_OBJECT_TYPE_ACTION`, `SEC_ROLE`, `SEC_USER`, `SEC_USER_ROLE`, `SEC_USER_TYPE`).

### Соседняя БД (не ElectronicQueueProf)

`UserDb` на том же инстансе SQL — пользователи, роли и права веб-приложения ([AppDbContext.cs](../Data/AppDbContext.cs)).

---

## Расхождения схемы БД и EF (8 таблиц мониторинга)

| Таблица | Колонка в БД | В EF (`Eq*`) |
|---------|--------------|--------------|
| `Appointment` | `id_appointment`, `id_category`, `date_arrival`, `time_arrival`, `number`, `time_start_pause`, `priority`, `info`, `id_client`, `time_complete` | Да |
| `Appointment` | `id_status_app`, `num_pause_in_a_row`, `last_id_location` | **Нет** |
| `List_item` | `id_list_item`, `id_appointment`, `id_specialty`, `time_*`, `id_status_item`, `id_cabinet`, `time_call`, `service_time`, `id_doctor` | Да (`service_time` не используется в отчётах) |
| `List_item` | `fix_cabinet`, `item_order`, `result_received` | **Нет** |
| `Category` | `id_category`, `name`, `priority` | Да |
| `Category` | `id_setting` | Да (`EqCategory.IdSetting`) |
| `Category` | `letter`, `old` | **Нет** |
| `Setting_queue` | `id_setting`, `start_id_specialty`, `end_id_specialty` | Да (`EqSettingQueue`) |
| `Setting_queue` | `time_pause`, `critical_num_pause`, `name` | **Нет** |
| `Cabinet` | `id_cabinet`, `cabinet_number` | Да |
| `Cabinet` | `id_board`, `ip_address_board`, `id_location`, `cabinet_display_number` | **Нет** |
| `Specialty` | `id_specialty`, `definition`, `time_servicing` (int, мин) | Да (`TimeServicing` int) |
| `Specialty` | `reload`, `priority`, `wait_for_result` | **Нет** |
| `Doctor`, `Log_work`, `Status_item_list` | все колонки, используемые в DbContext | Соответствуют |

Порядок этапов маршрута в коде: по возрастанию `id_list_item` (не `item_order`).

---

## Связи (внешние ключи)

| FK | Дочерняя таблица.колонка | Родитель |
|----|--------------------------|----------|
| FK_APPOINTM_LAST_ID_L_LOCATION | Appointment.last_id_location | Location.id_location |
| FK_APPOINTM_POSSESS_STATUS_A | Appointment.id_status_app | Status_Appointment.id_status_app |
| FK_APPOINTM_SERVICING_CATEGORY | Appointment.id_category | Category.id_category |
| FK_CABINET_DISPLAY_BOARD | Cabinet.id_board | Board.id_board |
| FK_CABINET_LOCATE_LOCATION | Cabinet.id_location | Location.id_location |
| FK_CATEGORY_HAVE_CONF_SETTING_ | Category.id_setting | Setting_queue.id_setting |
| FK_LIST_ITE_CURRENT_W_DOCTOR | List_item.id_doctor | Doctor.id_doctor |
| FK_LIST_ITE_HAVE_STATUS_I | List_item.id_status_item | Status_item_list.id_status_item |
| FK_LIST_ITE_NEED_EXEC_APPOINTM | List_item.id_appointment | Appointment.id_appointment |
| FK_LIST_ITE_NEED_PASS_SPECIALT | List_item.id_specialty | Specialty.id_specialty |
| FK_LIST_ITE_VISITED_CABINET | List_item.id_cabinet | Cabinet.id_cabinet |
| FK_LOG_WORK_ACTUAL_WO_DOCTOR | Log_work.id_doctor | Doctor.id_doctor |
| FK_LOG_WORK_WORKED_CABINET | Log_work.id_cabinet | Cabinet.id_cabinet |
| FK_MEDIA_FORMAT_TYPE_MED | Media.id_type_media | Type_media.id_type_media |
| FK_PREVIOUS_PREV_SPEC_SPECIALT | Previous_specialty.prev_id_specialty | Specialty.id_specialty |
| FK_PREVIOUS_SPECIALTY_SPECIALT | Previous_specialty.id_specialty | Specialty.id_specialty |
| FK_REFER_NOW_SPECIALT | Refer.id_specialty | Specialty.id_specialty |
| FK_REFER_TO_CABINET | Refer.id_cabinet | Cabinet.id_cabinet |
| FK_SETTING__ENDING_SPECIALT | Setting_queue.end_id_specialty | Specialty.id_specialty |
| FK_SETTING__STARTING_SPECIALT | Setting_queue.start_id_specialty | Specialty.id_specialty |
| FK_SHOW_ON_BOARD | Show.id_board | Board.id_board |
| FK_SHOW_PLACE_ZONE | Show.id_zone | Zone.id_zone |
| FK_SHOW_USING_MEDIA | Show.id_media | Media.id_media |
| SEC_* | (см. скрипт export) | внутренняя модель SEC |

Ядро процесса обслуживания (мониторинг):

```
Category ──< Appointment ──< List_item >── Specialty
                              │              Status_item_list
                              ├── Doctor (nullable FK)
                              └── Cabinet (nullable FK)
Doctor, Cabinet ──< Log_work
```

---

## Полная схема таблиц (dbo)

Формат: **колонка** `тип` NULL/NOT NULL, default.

### Appointment

**PK:** `id_appointment`

| Колонка | Тип | Null | Default |
|---------|-----|------|---------|
| id_appointment | int | NO | |
| id_status_app | int | NO | |
| id_category | int | YES | |
| date_arrival | date | NO | |
| time_arrival | time | NO | |
| number | varchar(32) | NO | |
| num_pause_in_a_row | int | NO | |
| time_start_pause | time | YES | |
| priority | int | NO | |
| info | varchar(512) | YES | |
| id_client | int | NO | |
| time_complete | time | YES | |
| last_id_location | int | YES | |

### Board

**PK:** `id_board`

| Колонка | Тип | Null | Default |
|---------|-----|------|---------|
| id_board | int | NO | |
| name_board | varchar(50) | NO | |
| num_show_rec | int | NO | |
| num_column | int | NO | (1) |
| num_col_end | int | NO | |

### Cabinet

**PK:** `id_cabinet`

| Колонка | Тип | Null | Default |
|---------|-----|------|---------|
| id_cabinet | int | NO | |
| id_board | int | NO | |
| cabinet_number | varchar(7) | YES | |
| ip_address_board | varchar(100) | YES | |
| id_location | int | YES | |
| cabinet_display_number | varchar(7) | YES | |

### Category

**PK:** `id_category`

| Колонка | Тип | Null | Default |
|---------|-----|------|---------|
| id_category | int | NO | |
| id_setting | int | NO | |
| name | varchar(64) | NO | |
| priority | int | NO | |
| letter | varchar(1) | NO | |
| old | bit | NO | (1) |

### Doctor

**PK:** `id_doctor`

| Колонка | Тип | Null | Default |
|---------|-----|------|---------|
| id_doctor | int | NO | |
| full_name | varchar(150) | NO | |

### List_item

**PK:** `id_list_item`

| Колонка | Тип | Null | Default |
|---------|-----|------|---------|
| id_appointment | int | NO | |
| id_specialty | int | NO | |
| time_start_servicing | time | YES | |
| time_end_servicing | time | YES | |
| id_list_item | int | NO | |
| id_status_item | int | NO | |
| id_cabinet | int | YES | |
| time_call | time | YES | |
| service_time | time | YES | |
| fix_cabinet | bit | NO | |
| item_order | int | YES | |
| id_doctor | int | YES | |
| result_received | bit | NO | |

### Location

**PK:** `id_location`

| Колонка | Тип | Null | Default |
|---------|-----|------|---------|
| id_location | int | NO | |
| name_location | varchar(200) | NO | |
| voice_location | varchar(100) | YES | |
| text_location | varchar(100) | YES | |

### Log_work

**PK:** `id_log_work`

| Колонка | Тип | Null | Default |
|---------|-----|------|---------|
| id_cabinet | int | NO | |
| id_log_work | int | NO | |
| id_doctor | int | NO | |
| date_work | date | NO | |
| time_begin | time | NO | |
| time_end | time | YES | |
| last_refresh | time | YES | |

### Media

**PK:** `id_media`

| Колонка | Тип | Null | Default |
|---------|-----|------|---------|
| id_media | int | NO | |
| id_type_media | int | NO | |
| data | varchar(1024) | NO | |

### Numbers

Таблица без объявленного PRIMARY KEY в метаданных.

| Колонка | Тип | Null | Default |
|---------|-----|------|---------|
| Number | int | NO | |

### Previous_specialty

**PK:** `id_prev_specialty`

| Колонка | Тип | Null | Default |
|---------|-----|------|---------|
| id_prev_specialty | int | NO | |
| id_specialty | int | NO | |
| prev_id_specialty | int | NO | |

### Refer

**PK:** `id_specialty`, `id_cabinet`

| Колонка | Тип | Null | Default |
|---------|-----|------|---------|
| id_specialty | int | NO | |
| id_cabinet | int | NO | |
| reload | bit | NO | (1) |

### SEC_ACE

**PK:** `SEC_ACE_ID` — колонки: SEC_ACE_ID, SEC_ROLE_ID, SEC_OBJ_ID, SEC_ACT_ID (int, NOT NULL).

### SEC_ACTION

**PK:** `SEC_ACT_ID` — SEC_ACT_ID, SEC_ACT_NAME varchar(50).

### SEC_LOGEVENT

**PK:** `SEC_LOG_ID` — SEC_LOG_ID, SEC_ACT_ID, SEC_OBJ_ID, SEC_USER_ID (nullable), SEC_LOG_DATETIME, SEC_LOG_USER, SEC_LOG_OBJECT_ID, SEC_LOG_OLD_VALUE, SEC_LOG_OBJECT_INFO.

### SEC_LOGIN

**PK:** `SEC_LOGIN_ID` — SEC_LOGIN_ID, SEC_USER_ID, SEC_LOGIN_NAME, SEC_LOGIN_DOMAIN, SEC_LOGIN_COMPUTER.

### SEC_OBJECT

**PK:** `SEC_OBJ_ID` — SEC_OBJ_ID, SEC_OBJ_TYPE_ID, SEC_OBJ_NAME.

### SEC_OBJECT_TYPE

**PK:** `SEC_OBJ_TYPE_ID` — SEC_OBJ_TYPE_ID, SEC_OBJ_TYPE_NAME.

### SEC_OBJECT_TYPE_ACTION

**PK:** `SEC_OBJ_TYPE_ID`, `SEC_ACT_ID`

### SEC_ROLE

**PK:** `SEC_ROLE_ID` — SEC_ROLE_ID, SEC_ROLE_NAME, SEC_ROLE_BUILTIN.

### SEC_USER

**PK:** `SEC_USER_ID` — SEC_USER_ID, WORK_ID, SEC_USER_TYPE_ID, SEC_USER_LOGIN, SEC_USER_PASSWORD, SEC_USER_FIO, SEC_USER_BUILTIN, SEC_USER_DISABLED, SEC_USER_NO_CHECK, SEC_USER_KKM_*.

### SEC_USER_ROLE

**PK:** `SEC_USER_ID`, `SEC_ROLE_ID`

### SEC_USER_TYPE

**PK:** `SEC_USER_TYPE_ID` — SEC_USER_TYPE_ID, SEC_USER_TYPE_NAME.

### Setting_queue

**PK:** `id_setting`

| Колонка | Тип | Null | Default |
|---------|-----|------|---------|
| end_id_specialty | int | YES | |
| start_id_specialty | int | YES | |
| time_pause | int | NO | |
| critical_num_pause | int | NO | |
| name | varchar(64) | YES | |
| id_setting | int | NO | |

### Shift

**PK:** `id_shift`

| Колонка | Тип | Null | Default |
|---------|-----|------|---------|
| id_shift | int | NO | |
| name | varchar(100) | NO | |
| begin_time | datetime | NO | |
| end_time | datetime | NO | |

### Show

**PK:** `id_show`

| Колонка | Тип | Null | Default |
|---------|-----|------|---------|
| id_board | int | NO | |
| id_media | int | NO | |
| id_show | int | NO | |
| id_zone | int | NO | |

### Specialty

**PK:** `id_specialty`

| Колонка | Тип | Null | Default |
|---------|-----|------|---------|
| id_specialty | int | NO | |
| definition | varchar(128) | NO | |
| time_servicing | int | NO | (5) |
| reload | bit | NO | (1) |
| priority | int | YES | |
| wait_for_result | bit | NO | (0) |

### Status_Appointment

**PK:** `id_status_app`

| Колонка | Тип | Null | Default |
|---------|-----|------|---------|
| id_status_app | int | NO | |
| name | varchar(32) | NO | |

### Status_item_list

**PK:** `id_status_item`

| Колонка | Тип | Null | Default |
|---------|-----|------|---------|
| id_status_item | int | NO | |
| name | varchar(64) | YES | |

### Type_media

**PK:** `id_type_media`

| Колонка | Тип | Null | Default |
|---------|-----|------|---------|
| id_type_media | int | NO | |
| description | varchar(20) | NO | |

### Version

**PK:** `version`

| Колонка | Тип | Null | Default |
|---------|-----|------|---------|
| version | int | NO | |
| date | date | NO | |

### Zone

**PK:** `id_zone`

| Колонка | Тип | Null | Default |
|---------|-----|------|---------|
| id_zone | int | NO | |
| description | varchar(32) | NO | |
| translate | varchar(32) | NO | |

---

## См. также

- [report-db-field-mapping.md](report-db-field-mapping.md) — поля БД по отчётам каталога
- [report-catalog-contracts.md](report-catalog-contracts.md) — контракты генерации отчётов
