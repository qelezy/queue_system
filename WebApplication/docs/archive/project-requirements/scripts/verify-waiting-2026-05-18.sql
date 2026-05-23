-- Spot-check: «Ожидание до приёма» for 2026-05-18 (hourly agg + top outliers).
-- Usage: sqlcmd -S localhost\SQLEXPRESS01 -E -d ElectronicQueueProf -i verify-waiting-2026-05-18.sql
-- Compare hourly count/avg/min/max with report table for that day (workday rows start at hour 8).

DECLARE @day date = '2026-05-18';

PRINT '=== Waiting before appointment spot-check: ' + CONVERT(varchar(10), @day, 120) + ' ===';

IF OBJECT_ID('tempdb..#valid') IS NOT NULL DROP TABLE #valid;

;WITH stages AS (
    SELECT
        li.id_list_item,
        li.id_appointment,
        a.date_arrival,
        a.time_arrival,
        li.time_call,
        li.time_start_servicing,
        li.time_end_servicing,
        ROW_NUMBER() OVER (
            PARTITION BY li.id_appointment
            ORDER BY COALESCE(li.time_start_servicing, '23:59:59'), li.id_list_item
        ) AS stage_rn,
        COUNT(*) OVER (PARTITION BY li.id_appointment) AS stage_count
    FROM List_item li
    INNER JOIN Appointment a ON li.id_appointment = a.id_appointment
    WHERE a.date_arrival = @day
      AND li.time_call IS NOT NULL
),
wait_calc AS (
    SELECT
        s.*,
        DATEPART(HOUR, s.time_arrival) AS arrival_hour,
        CASE
            WHEN s.stage_rn = 1 THEN
                DATEDIFF(
                    SECOND,
                    CAST(CAST(s.date_arrival AS datetime) + CAST(s.time_arrival AS datetime) AS datetime),
                    CAST(CAST(s.date_arrival AS datetime) + CAST(s.time_call AS datetime) AS datetime)) / 60.0
            WHEN prev.time_end_servicing IS NOT NULL THEN
                DATEDIFF(
                    SECOND,
                    CAST(CAST(s.date_arrival AS datetime) + CAST(prev.time_end_servicing AS datetime) AS datetime),
                    CAST(CAST(s.date_arrival AS datetime) + CAST(s.time_call AS datetime) AS datetime)) / 60.0
            WHEN prev.time_start_servicing IS NOT NULL THEN
                DATEDIFF(
                    SECOND,
                    CAST(CAST(s.date_arrival AS datetime) + CAST(prev.time_start_servicing AS datetime) AS datetime),
                    CAST(CAST(s.date_arrival AS datetime) + CAST(s.time_call AS datetime) AS datetime)) / 60.0
            ELSE NULL
        END AS wait_new_min
    FROM stages s
    LEFT JOIN stages prev
        ON prev.id_appointment = s.id_appointment
       AND prev.stage_rn = s.stage_rn - 1
)
SELECT *
INTO #valid
FROM wait_calc
WHERE wait_new_min IS NOT NULL
  AND wait_new_min >= 0
  AND wait_new_min < 10080;

SELECT
    arrival_hour,
    COUNT(*) AS completed_waits,
    ROUND(AVG(wait_new_min), 1) AS avg_wait_min,
    ROUND(MIN(wait_new_min), 1) AS min_wait_min,
    ROUND(MAX(wait_new_min), 1) AS max_wait_min
FROM #valid
WHERE arrival_hour BETWEEN 8 AND 18
GROUP BY arrival_hour
ORDER BY arrival_hour;

SELECT
    COUNT(*) AS day_total_waits_workday,
    ROUND(AVG(wait_new_min), 1) AS day_avg_wait_min,
    ROUND(MIN(wait_new_min), 1) AS day_min_wait_min,
    ROUND(MAX(wait_new_min), 1) AS day_max_wait_min
FROM #valid
WHERE arrival_hour BETWEEN 8 AND 18;

PRINT '=== Top 10 waits in arrival hour 8 (outliers) ===';

SELECT TOP 10
    id_appointment,
    id_list_item,
    stage_rn,
    stage_count,
    time_arrival,
    time_call,
    time_start_servicing,
    time_end_servicing,
    ROUND(wait_new_min, 1) AS wait_new_min
FROM #valid
WHERE arrival_hour = 8
ORDER BY wait_new_min DESC;

PRINT '=== Stage order ambiguity: same start, multiple list items ===';

SELECT
    li.id_appointment,
    li.time_start_servicing,
    COUNT(*) AS stages_same_start
FROM List_item li
INNER JOIN Appointment a ON li.id_appointment = a.id_appointment
WHERE a.date_arrival = @day
  AND li.time_call IS NOT NULL
GROUP BY li.id_appointment, li.time_start_servicing
HAVING COUNT(*) > 1;

DROP TABLE #valid;
