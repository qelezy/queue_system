-- Baseline counters for report smoke verification (ElectronicQueueProf).
-- Usage: sqlcmd -S localhost\SQLEXPRESS01 -E -d ElectronicQueueProf -i verify-reports-baseline.sql
-- Default window matches WebApplication.Tests ReportsSmokeTests period.

DECLARE @from date = '2026-05-01';
DECLARE @to date = '2026-05-19';

PRINT '=== Period ' + CONVERT(varchar(10), @from, 120) + ' .. ' + CONVERT(varchar(10), @to, 120) + ' ===';

SELECT
    COUNT(*) AS appointments_in_period
FROM Appointment
WHERE date_arrival BETWEEN @from AND @to;

SELECT
    COUNT(*) AS appointments_without_list_item
FROM Appointment a
WHERE a.date_arrival BETWEEN @from AND @to
  AND NOT EXISTS (
      SELECT 1 FROM List_item li WHERE li.id_appointment = a.id_appointment
  );

SELECT
    COUNT(*) AS list_items_with_start_and_end
FROM List_item li
INNER JOIN Appointment a ON li.id_appointment = a.id_appointment
WHERE a.date_arrival BETWEEN @from AND @to
  AND li.time_start_servicing IS NOT NULL
  AND li.time_end_servicing IS NOT NULL;

SELECT
    COUNT(*) AS log_work_with_bounds
FROM Log_work
WHERE date_work BETWEEN @from AND @to
  AND time_begin IS NOT NULL
  AND time_end IS NOT NULL;

SELECT
    COUNT(*) AS list_items_null_doctor
FROM List_item li
INNER JOIN Appointment a ON li.id_appointment = a.id_appointment
WHERE a.date_arrival BETWEEN @from AND @to
  AND (li.id_doctor IS NULL OR li.id_doctor = 0);

SELECT
    COUNT(*) AS list_items_null_cabinet
FROM List_item li
INNER JOIN Appointment a ON li.id_appointment = a.id_appointment
WHERE a.date_arrival BETWEEN @from AND @to
  AND (li.id_cabinet IS NULL OR li.id_cabinet = 0);

SELECT
    COUNT(*) AS multi_stage_appointments
FROM (
    SELECT li.id_appointment
    FROM List_item li
    INNER JOIN Appointment a ON li.id_appointment = a.id_appointment
    WHERE a.date_arrival BETWEEN @from AND @to
    GROUP BY li.id_appointment
    HAVING COUNT(*) >= 2
) t;

SELECT
    COUNT(*) AS list_items_start_without_end
FROM List_item li
INNER JOIN Appointment a ON li.id_appointment = a.id_appointment
WHERE a.date_arrival BETWEEN @from AND @to
  AND li.time_start_servicing IS NOT NULL
  AND li.time_end_servicing IS NULL;

SELECT
    COUNT(*) AS list_items_with_servicing_no_matching_log_work
FROM List_item li
INNER JOIN Appointment a ON li.id_appointment = a.id_appointment
WHERE a.date_arrival BETWEEN @from AND @to
  AND li.time_start_servicing IS NOT NULL
  AND li.time_end_servicing IS NOT NULL
  AND li.id_doctor IS NOT NULL AND li.id_doctor > 0
  AND li.id_cabinet IS NOT NULL AND li.id_cabinet > 0
  AND NOT EXISTS (
      SELECT 1
      FROM Log_work lw
      WHERE lw.id_doctor = li.id_doctor
        AND lw.id_cabinet = li.id_cabinet
        AND lw.date_work = a.date_arrival
        AND lw.time_begin IS NOT NULL
        AND lw.time_end IS NOT NULL
  );

PRINT '=== Wait before call: old vs new formula ===';

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
    WHERE a.date_arrival BETWEEN @from AND @to
      AND li.time_call IS NOT NULL
),
wait_calc AS (
    SELECT
        s.*,
        CASE
            WHEN s.stage_rn = 1 THEN
                DATEDIFF(
                    MINUTE,
                    CAST(CAST(s.date_arrival AS datetime) + CAST(s.time_arrival AS datetime) AS datetime),
                    CAST(CAST(s.date_arrival AS datetime) + CAST(s.time_call AS datetime) AS datetime))
            WHEN prev.time_end_servicing IS NOT NULL THEN
                DATEDIFF(
                    MINUTE,
                    CAST(CAST(s.date_arrival AS datetime) + CAST(prev.time_end_servicing AS datetime) AS datetime),
                    CAST(CAST(s.date_arrival AS datetime) + CAST(s.time_call AS datetime) AS datetime))
            WHEN prev.time_start_servicing IS NOT NULL THEN
                DATEDIFF(
                    MINUTE,
                    CAST(CAST(s.date_arrival AS datetime) + CAST(prev.time_start_servicing AS datetime) AS datetime),
                    CAST(CAST(s.date_arrival AS datetime) + CAST(s.time_call AS datetime) AS datetime))
            ELSE NULL
        END AS wait_new_min,
        DATEDIFF(
            MINUTE,
            CAST(CAST(s.date_arrival AS datetime) + CAST(s.time_arrival AS datetime) AS datetime),
            CAST(CAST(s.date_arrival AS datetime) + CAST(s.time_call AS datetime) AS datetime)) AS wait_old_min
    FROM stages s
    LEFT JOIN stages prev
        ON prev.id_appointment = s.id_appointment
       AND prev.stage_rn = s.stage_rn - 1
)
SELECT
    COUNT(*) AS stages_with_call,
    AVG(CAST(wait_old_min AS float)) AS avg_old,
    AVG(CAST(wait_new_min AS float)) AS avg_new,
    MAX(wait_old_min) AS max_old,
    MAX(wait_new_min) AS max_new,
    AVG(CASE WHEN stage_count >= 2 THEN 100.0 ELSE 0 END) AS pct_stages_multi
FROM wait_calc
WHERE wait_new_min IS NOT NULL
  AND wait_new_min >= 0
  AND wait_new_min < 10080;

SELECT TOP 10
    id_appointment,
    stage_rn,
    wait_old_min,
    wait_new_min,
    wait_old_min - wait_new_min AS delta_min
FROM wait_calc
WHERE wait_new_min IS NOT NULL
  AND wait_old_min IS NOT NULL
  AND wait_old_min <> wait_new_min
ORDER BY ABS(wait_old_min - wait_new_min) DESC;

PRINT '=== Appointment duration: stage vs appointment aggregates ===';

SELECT
    COUNT(*) AS completed_stages,
    AVG(CAST(DATEDIFF(
        MINUTE,
        CAST(CAST(a.date_arrival AS datetime) + CAST(li.time_start_servicing AS datetime) AS datetime),
        CAST(CAST(a.date_arrival AS datetime) + CAST(li.time_end_servicing AS datetime) AS datetime)
    ) AS float)) AS avg_stage_min,
    MIN(DATEDIFF(
        MINUTE,
        CAST(CAST(a.date_arrival AS datetime) + CAST(li.time_start_servicing AS datetime) AS datetime),
        CAST(CAST(a.date_arrival AS datetime) + CAST(li.time_end_servicing AS datetime) AS datetime)
    )) AS min_stage_min,
    MAX(DATEDIFF(
        MINUTE,
        CAST(CAST(a.date_arrival AS datetime) + CAST(li.time_start_servicing AS datetime) AS datetime),
        CAST(CAST(a.date_arrival AS datetime) + CAST(li.time_end_servicing AS datetime) AS datetime)
    )) AS max_stage_min,
    COUNT(DISTINCT li.id_appointment) AS distinct_appointments
FROM List_item li
INNER JOIN Appointment a ON li.id_appointment = a.id_appointment
WHERE a.date_arrival BETWEEN @from AND @to
  AND li.time_start_servicing IS NOT NULL
  AND li.time_end_servicing IS NOT NULL
  AND DATEDIFF(
        MINUTE,
        CAST(CAST(a.date_arrival AS datetime) + CAST(li.time_start_servicing AS datetime) AS datetime),
        CAST(CAST(a.date_arrival AS datetime) + CAST(li.time_end_servicing AS datetime) AS datetime)
      ) >= 0
  AND DATEDIFF(
        MINUTE,
        CAST(CAST(a.date_arrival AS datetime) + CAST(li.time_start_servicing AS datetime) AS datetime),
        CAST(CAST(a.date_arrival AS datetime) + CAST(li.time_end_servicing AS datetime) AS datetime)
      ) < 10080;
