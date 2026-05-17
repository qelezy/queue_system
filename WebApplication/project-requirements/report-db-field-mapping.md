# Матрица полей БД → отчёты каталога (ElectronicQueueProf)

Эталон схемы: [electronic-queue-prof-schema.md](electronic-queue-prof-schema.md) (полная БД + таблицы мониторинга). Краткий перечень полей: [electronic-queue-description.txt](electronic-queue-description.txt). Маппинг EF — [ElectronicQueueDbContext.cs](../Data/ElectronicQueueDbContext.cs).

**Не используется в отчётах каталога (как правило):** `Appointment.time_start_pause`, `priority`, `id_client`, `List_item.service_time` (кроме если явно не указано в spec), `Log_work.last_refresh`, `Specialty.time_servicing` (норматив, не факт).

---

## `load-and-downtime`

| Колонка | Источник |
|---------|----------|
| Дата | `Log_work.date_work` |
| Интервал работы | merge `Log_work.time_begin` / `time_end` по (врач, кабинет, день), обрезка периода |
| Врач / Кабинет | `Doctor.full_name`, `Cabinet.cabinet_number` (`CustomParams.analysisMode`) |
| Специализация врача | уникальные `Specialty.definition` этапов `List_item` смены |
| Длительность рабочего времени, мин | сумма окон `Log_work` |
| Общая длительность обслуживания, мин | merge `List_item.time_start_servicing`–`time_end_servicing` в окне |
| Простои, загрузка %, число завершённых приёмов | производные; завершённый талон — `List_item` с `time_call`, конец обслуживания в периоде, `date_arrival` = `date_work` |

Таблицы: `Log_work`, `List_item`, `Appointment`, `Doctor`, `Cabinet`, `Specialty`. `Status_item_list` — join без фильтра по имени «неяв».

---

## `arrived-and-completed`

| Колонка | Источник |
|---------|----------|
| Дата | `Appointment.date_arrival` |
| Категория | `Category.name` |
| Зарегистрированных приёмов | count `Appointment` в (день, категория) |
| Неявок | `Appointment` без строк `List_item` |
| Завершённый маршрут / незавершённый | все этапы талона: `time_end_servicing` заполнен / есть пустой |

Таблицы: `Appointment`, `List_item`, `Category`.

---

## `waiting-before-appointment`

| Колонка | Источник |
|---------|----------|
| Дата | `Appointment.date_arrival` |
| Интервал | час `Appointment.time_arrival` (обрезка `DateFrom`/`DateTo`) |
| Метрики ожидания | `Combine(date_arrival, time_call)` − `Combine(date_arrival, time_arrival)`; отбор `time_call` ∈ период |

Таблицы: `Appointment`, `List_item` (`time_call`).

---

## `appointment-duration`

| Колонка | Источник |
|---------|----------|
| Дата | `Appointment.date_arrival` (группировка по дню) |
| Срез (врач / специальность / кабинет) | `Doctor.full_name` / `Specialty.definition` / `Cabinet.cabinet_number` (`CustomParams.analysisMode`) |
| Специализация врача | уникальные `Specialty.definition` этапов строки (только `analysisMode=doctor`) |
| Завершённых приёмов | distinct `Appointment.id_appointment` среди этапов с парой `time_start_servicing`, `time_end_servicing` |
| Средняя длительность | разница Combine на `date_arrival`; среднее по этапам ячейки |
| Норматив, мин | среднее `Specialty.time_servicing` по этапам ячейки |
| Отклонение, мин | средняя длительность − средний норматив |
| Минимум, мин | min длительности по этапам ячейки |
| Максимум, мин | max длительности по этапам ячейки |

Период: `date_arrival` ∈ [`fromDo`, `toDo`]. **Не** `service_time`. **Итого по врачам/специальностям/кабинетам** — подитог по срезу за период (без «Итого за день»). `groupedBar` — топ-8 срезов × дни.

---

## `route-and-pauses`

Период: `periodFrom`/`periodTo`; предзагрузка `date_arrival` ∈ [`fromDo`, `toDo`]; метрики — пересечение с периодом (обрезка).

| Колонка | Источник |
|---------|----------|
| Дата | `Appointment.date_arrival` |
| Пациент | `Appointment.info` |
| Интервал полного обслуживания | от `time_call` (fallback `time_start_servicing`, `time_arrival`) первого этапа до `time_end_servicing` (fallback `time_complete`) последнего; отображение — clip к периоду, `HH:mm–HH:mm` |
| Этапов | count `List_item` приёма (≥2; пересечение маршрута с периодом) |
| Суммарное время прохождения | clip суммы `[time_start_servicing, time_end_servicing]` по этапам |
| Сумма пауз | clip суммы пауз между `time_end_servicing` и следующим `time_start_servicing` |

Превью: groupedBar (суммы по дням); итоги — строка «Итого за период» в таблице.

---

## `unserved-and-chain-breaks`

| Колонка | Источник |
|---------|----------|
| Дата | `Appointment.date_arrival` |
| Категория обслуживания | `Category.name` |
| Зарегистрированных приёмов | count distinct `id_appointment` |
| Неявок на приёмы | приёмы без `List_item` |
| Приёмов с незавершённым обслуживанием | есть этап без `time_end_servicing` |
| Доля приёмов с неявкой или незавершённым обслуживанием, % | вычисляемое |

Таблицы: `Appointment`, `Category`, `List_item`.

Подробности — [report-unserved-and-chain-breaks-spec.md](report-unserved-and-chain-breaks-spec.md).
---

## `service-categories-comparison`

| Колонка | Источник |
|---------|----------|
| Категория | `Category.name` |
| Приёмов | distinct `Appointment.id_appointment` с этапами |
| Среднее / мин / макс ожидание | как waiting, по этапам категории |
| Средняя / мин / макс длительность приёма | как appointment-duration, по этапам |
| Диаграмма одно/многоэтапных маршрутов | C = count `List_item` на `id_appointment` (=1 / ≥2) |

---

## `service-delays`

Период: `date_arrival` ∈ [`fromDo`, `toDo`]. Срез: `CustomParams.analysisMode` → `doctor` | `cabinet`. Строка = одна сущность.

**Срез `doctor`:**

| Колонка | Источник |
|---------|----------|
| Врач | `Doctor.full_name` |
| Специализация | уникальные `Specialty.definition` этапов врача, `"; "` |
| Инцидентов задержки | этапы с `stage_delay_min > 0` |
| Сумма задержек, минут | задержка после вызова + сверх `Specialty.time_servicing` |
| Средняя задержка, минут | сумма / инциденты |
| Минимальная задержка, минут | min `stage_delay_min` по инцидентам |
| Максимальная задержка, минут | max `stage_delay_min` по инцидентам |
| Превышений норматива | этапы с `(time_end − time_start) > time_servicing` |

**Срез `cabinet`:** колонки «Кабинет» (`Cabinet.cabinet_number`), «Специализация врача» (те же `Specialty.definition` по этапам кабинета); остальные метрики — как выше.

Топ 15 по «Сумма задержек, минут» (порядок строк без колонки №).
