# Отчёт «Исходы обслуживания» (`service-route-outcomes`)

Сводка по **приёмам за период**: число приёмов, завершённые и незавершённые маршруты по дням и категориям. Реализация: `ServiceRouteOutcomesReportGenerator`, `ServiceRouteOutcomesReportBuilder`.

Заменяет отчёты `arrived-and-completed` и `no-shows-and-incomplete-service` (superseded). **Колонки неявок нет.**

Паузы и интервалы маршрута — [`route-and-pauses`](report-route-and-pauses-spec.md).

---

## Период и отбор

- Период: `ReportGenerateRequest.DateFrom` / `DateTo` (UTC-строки).
- Фильтр: `Appointment.date_arrival` ∈ [`fromDo`, `toDo`] (`CatalogReportShared.ParsePeriod`).
- Строка таблицы: календарный `date_arrival` × `Category.name`.

---

## Столбцы (5)

| # | Колонка | Определение |
|---|---------|-------------|
| 1 | Дата | `date_arrival`, формат `yyyy-MM-dd`; повторы дня в группе — пустая ячейка (rowspan в HTML/PDF/превью) |
| 2 | Категория обслуживания | `Category.name` |
| 3 | Приёмов | count distinct `Appointment.id_appointment` в (день, категория) |
| 4 | С завершённым маршрутом | ≥1 `List_item` и у **всех** этапов задан `time_end_servicing` |
| 5 | С незавершённым обслуживанием | ≥1 `List_item` и **хотя бы у одного** этапа `time_end_servicing` пуст |

Приёмы **без** строк `List_item` учитываются только в колонке «Приёмов»; в колонках 4–5 не попадают.

Классификация — **только по полям времени**, не по имени статуса «неяв».

---

## Итоги и экспорт

- Блок **«Итого за период»** в конце таблицы (суммы по колонкам 3–5).
- CSV/HTML/PDF: имя файла `service-route-outcomes.csv`; группировка даты с `rowspan` как в прежних сводных отчётах.

---

## Диаграммы превью

1. **doughnut** — суммы за период: «С завершённым маршрутом» + «С незавершённым обслуживанием» (`ForServiceRouteOutcomesMix`).
2. **groupedBar** по дням — две серии (те же метрики), без неявок (`ForServiceRouteOutcomesDailyGroupedBar`).

---

## Mock и live

- Live: `CatalogAppointmentDataLoader` + `ServiceRouteOutcomesReportBuilder`.
- Mock: `GenerateServiceRouteOutcomesOffline` → те же наблюдения (`MockReportOfflineSeed.BuildArrivedAndCompletedData`).

Идентификатор permission и каталога: `service-route-outcomes`.
