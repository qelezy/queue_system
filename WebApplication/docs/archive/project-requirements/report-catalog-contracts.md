# Контракты отчётов каталога (Reports:Catalog)

## Схема `appsettings.json` → `Reports:Catalog`

Минимальная запись: `Id`, `Category`, `Title`, `Description`. Технические поля (`GeneratorKind`, `TableLayout`, `PdfOrientation`, `DetailRowKind`) выводятся из `Id` через [`ReportCatalogDefaults`](../../../Models/Reports/Configuration/ReportCatalogDefaults.cs) (карта `ReportIds` → `ReportGeneratorKind`, затем презентация по kind). Переопределения в JSON — опционально, если дефолт не подходит.

Общее: период из `ReportGenerateRequest.DateFrom` / `DateTo` (UTC-строки); фильтр по `DateArrival` приёма (`Appointment`). Для JSON-превью строки таблицы усечены до `ReportPreviewLimits.MaxTableRows`. Экспорт — полная таблица. Матрица полей БД — [report-db-field-mapping.md](report-db-field-mapping.md). Mock и live используют общие `*ReportBuilder` (синтетические наблюдения в `Services/Demo/MockReportOfflineSeed`, только Development).

## `load-and-downtime`

См. [report-load-and-downtime-spec.md](report-load-and-downtime-spec.md). `CustomParams.analysisMode`: `doctor` | `cabinet`. Интервалы занятости по завершённым этапам (`List_item` с началом и концом обслуживания) учитываются по полям времени без фильтра по имени статуса «неяв». Поля БД — [report-db-field-mapping.md](report-db-field-mapping.md). Mock: `GenerateLoadAndDowntimeOffline` → `LoadAndDowntimeReportBuilder`.

## `service-route-outcomes` (`ReportIds.ServiceRouteOutcomes`)

См. [report-service-route-outcomes-spec.md](report-service-route-outcomes-spec.md). Строка = календарный `DateArrival` × категория; **5** столбцов: приёмов, с завершённым маршрутом, с незавершённым обслуживанием (плюс дата и категория). Без колонки неявок. `PreviewCharts` — doughnut + groupedBar по дням (`ReportPreviewChartDescriptors.ForServiceRouteOutcomesCharts`). Период по `DateArrival`. Mock: `GenerateServiceRouteOutcomesOffline` → `ServiceRouteOutcomesReportBuilder`.

## `waiting-before-appointment` (`ReportIds.WaitingBeforeAppointment`)

См. [report-waiting-before-appointment-spec.md](report-waiting-before-appointment-spec.md). **6** столбцов: дата, интервал (обрезка по `DateFrom`/`DateTo` на краях периода), метрики до `time_call`. Отбор по `time_call` ∈ период; разрез часа — `time_arrival.Hour`. Итог за день + **Итого за период**. `groupedBar` по часам из таблицы. Mock: `GenerateWaitingBeforeAppointmentOffline`.

## `appointment-duration` (`ReportIds.ServiceDurationDistribution`)

См. [report-appointment-duration-spec.md](report-appointment-duration-spec.md). **8–9** столбцов: дата, срез (`CustomParams.analysisMode`: `doctor` | `specialty` | `cabinet`), в режиме `doctor` — «Специализация врача», **завершённые приёмы** (distinct талон), длительность, норматив, отклонение, мин/макс. **Итого по врачам/специальностям/кабинетам** — по строке на срез за период (без «Итого за день»). `groupedBar`: топ-8 срезов × дни; в столбце среза — наложение среднего и норматива. Период по `DateArrival`. Mock: `GenerateAppointmentDurationOffline`.

## `route-and-pauses` (`ReportIds.RouteAndPauses`)

См. [report-route-and-pauses-spec.md](report-route-and-pauses-spec.md). **5** столбцов (без id и без персональных данных): многоэтапные приёмы с пересечением маршрута с `periodFrom`/`periodTo`; интервал полного обслуживания (`time_arrival` → `time_end_servicing` последнего этапа); сумма обслуживания по этапам и сумма пауз call→start с обрезкой; группировка по дате; **«Итого за период»** в конце таблицы. `PreviewCharts`: groupedBar по дням (`ForRouteAndPausesDailyGroupedBar`). Mock: `GenerateRouteAndPausesOffline` → `RouteAndPausesReportBuilder`.

## `service-categories-comparison` (`ReportIds.ServiceCategoriesComparison`)

См. [report-service-categories-comparison-spec.md](report-service-categories-comparison-spec.md). Строка = категория; **8** столбцов: приёмов, среднее/мин/макс ожидания и длительности. `PreviewCharts` — doughnut одно/многоэтапных маршрутов (`ForMultiStageRoutesMix`, C = count `List_item`). Без `CustomParams`. Mock: `GenerateServiceCategoriesComparisonOffline`.

## `service-delays` (`ReportIds.ServiceDelays`)

См. [report-service-delays-spec.md](report-service-delays-spec.md). `CustomParams.analysisMode`: `doctor` | `cabinet`. **8** столбцов (подписи сущности и специализации зависят от среза); строка = сущность; топ **15** по **`total_delay_min`**. Mock/live: `ServiceDelaysQueries` + `ServiceDelaysReportBuilder`. Mock: `GenerateServiceDelaysOffline`.
