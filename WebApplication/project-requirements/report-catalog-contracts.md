# Контракты отчётов каталога (Reports:Catalog)

Общее: период из `ReportGenerateRequest.DateFrom` / `DateTo` (UTC-строки); фильтр по `DateArrival` приёма (`Appointment`). Для JSON-превью строки таблицы усечены до `ReportPreviewLimits.MaxTableRows`. Экспорт — полная таблица. Матрица полей БД — [report-db-field-mapping.md](report-db-field-mapping.md). Mock и live используют общие `*ReportBuilder` (синтетические наблюдения в `MockReportOfflineSeed`). Во всех отчётах каталога **неявка на приём** — приём без ни одной строки `List_item` (см. `CatalogReportShared.CountAppointmentsWithoutListItems`); классификация этапов по времени **не** опирается на имя статуса «неяв» как на отдельную «неявку приёма».

## `load-and-downtime`

См. [report-load-and-downtime-spec.md](report-load-and-downtime-spec.md). `CustomParams.analysisMode`: `doctor` | `cabinet`. Интервалы занятости по завершённым этапам (`List_item` с началом и концом обслуживания) учитываются по полям времени без фильтра по имени статуса «неяв». Поля БД — [report-db-field-mapping.md](report-db-field-mapping.md). Mock: `GenerateLoadAndDowntimeOffline` → `LoadAndDowntimeReportBuilder`.

## `arrived-and-completed` (`ReportIds.FlowBalanceArrivedVsCompleted`)

См. [report-arrived-and-completed-spec.md](report-arrived-and-completed-spec.md). Строка = календарный `DateArrival` × категория; **6** столбцов: зарегистрированные приёмы, неявки, приёмы с завершённым маршрутом, приёмы с незавершённым обслуживанием (плюс дата и категория). Группировка даты и CSV — как в spec. `PreviewCharts` — doughnut по суммам колонок 4–6 за период (`ReportPreviewChartDescriptors.ForArrivedCompletedAppointmentMix`). Период по `DateArrival` (`CatalogReportShared.ParsePeriod`). Mock: `GenerateArrivedAndCompletedOffline` → `ArrivedAndCompletedReportBuilder`.

## `unserved-and-chain-breaks` (`ReportIds.UnservedChainBreaks`)

См. [report-unserved-and-chain-breaks-spec.md](report-unserved-and-chain-breaks-spec.md). **6** столбцов: сводка для менеджера (день × категория): приёмы, неявки, незавершённое обслуживание, доля с неявкой или незавершённым %. «Итого за период». `PreviewCharts`: doughnut (col.4–5) + `groupedBar` по дням. Баланс потока с колонкой «завершённый маршрут» — `arrived-and-completed`. Паузы — `route-and-pauses`. Mock/live: те же наблюдения, что у arrived; mock — `BuildArrivedAndCompletedData`.
## `waiting-before-appointment` (`ReportIds.WaitTimeDistribution`)

См. [report-waiting-before-appointment-spec.md](report-waiting-before-appointment-spec.md). **6** столбцов: дата, интервал (обрезка по `DateFrom`/`DateTo` на краях периода), метрики до `time_call`. Отбор по `time_call` ∈ период; разрез часа — `time_arrival.Hour`. Итог за день + **Итого за период**. `groupedBar` по часам из таблицы. Mock: `GenerateWaitingBeforeAppointmentOffline`.

## `appointment-duration` (`ReportIds.ServiceDurationDistribution`)

См. [report-appointment-duration-spec.md](report-appointment-duration-spec.md). **8–9** столбцов: дата, срез (`CustomParams.analysisMode`: `doctor` | `specialty` | `cabinet`), в режиме `doctor` — «Специализация врача», **завершённые приёмы** (distinct талон), длительность, норматив, отклонение, мин/макс. **Итого по врачам/специальностям/кабинетам** — по строке на срез за период (без «Итого за день»). `groupedBar`: топ-8 срезов × дни; в столбце среза — наложение среднего и норматива. Период по `DateArrival`. Mock: `GenerateAppointmentDurationOffline`.

## `route-and-pauses` (`ReportIds.FullCycleStageDelays`)

См. [report-route-and-pauses-spec.md](report-route-and-pauses-spec.md). **6** столбцов (без id): многоэтапные приёмы с пересечением маршрута с `periodFrom`/`periodTo`; интервал полного обслуживания (`time_call` → … → `time_end_servicing`); суммы прохождения и пауз с обрезкой; группировка по дате; **«Итого за период»** в конце таблицы. `PreviewCharts`: groupedBar по дням (`ForRouteAndPausesDailyGroupedBar`). Mock: `GenerateRouteAndPausesOffline` → `RouteAndPausesReportBuilder`.

## `service-categories-comparison` (`ReportIds.ServiceCategoriesPerformance`)

См. [report-service-categories-comparison-spec.md](report-service-categories-comparison-spec.md). Строка = категория; **8** столбцов: приёмов, среднее/мин/макс ожидания и длительности. `PreviewCharts` — doughnut одно/многоэтапных маршрутов (`ForMultiStageRoutesMix`, C = count `List_item`). Без `CustomParams`. Mock: `GenerateServiceCategoriesComparisonOffline`.

## `service-delays` (`ReportIds.ServiceDelays`)

См. [report-service-delays-spec.md](report-service-delays-spec.md). `CustomParams.analysisMode`: `doctor` | `cabinet`. **8** столбцов (подписи сущности и специализации зависят от среза); строка = сущность; топ **15** по **`total_delay_min`**. Mock/live: `BottleneckRankingQueries` + `BottleneckRankingReportBuilder`. Mock: `GenerateBottleneckRankingOffline`.
