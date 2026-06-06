-- Verify seed: 20 tickets, multiple source dates, today MSK (same as QueueDashboardClock)
SET NOCOUNT ON;

DECLARE @nowMsk datetime2 = SYSDATETIME();
DECLARE @todayMsk date = CAST(@nowMsk AS date);

PRINT '=== Verify ===';
PRINT 'Today (MSK): ' + CONVERT(varchar(10), @todayMsk, 120);
PRINT 'Now (MSK):   ' + CONVERT(varchar(30), @nowMsk, 120);

PRINT '--- Configurator: 20 tickets today, route_steps <= 3 ---';
SELECT a.number, a.id_status_app, a.info,
       COUNT(li.id_list_item) AS route_steps,
       MAX(CASE WHEN r.id_specialty IS NOT NULL THEN 1 ELSE 0 END) AS has_refer_step
FROM Appointment a
JOIN List_item li ON li.id_appointment = a.id_appointment
LEFT JOIN Refer r ON r.id_specialty = li.id_specialty AND r.id_cabinet = li.id_cabinet
WHERE a.id_client BETWEEN 99990001 AND 99990020
  AND a.date_arrival = @todayMsk
GROUP BY a.number, a.id_status_app, a.info
ORDER BY a.number;

SELECT COUNT(*) AS tickets_today FROM Appointment
WHERE id_client BETWEEN 99990001 AND 99990020 AND date_arrival = @todayMsk;

PRINT '--- Status coverage (appointment) ---';
SELECT a.id_status_app, COUNT(*) AS cnt
FROM Appointment a
WHERE a.id_client BETWEEN 99990001 AND 99990020 AND a.date_arrival = @todayMsk
GROUP BY a.id_status_app ORDER BY a.id_status_app;

PRINT '--- Status coverage (list_item current/max) ---';
SELECT li.id_status_item, COUNT(*) AS cnt
FROM List_item li
JOIN Appointment a ON a.id_appointment = li.id_appointment
WHERE a.id_client BETWEEN 99990001 AND 99990020 AND a.date_arrival = @todayMsk
GROUP BY li.id_status_item ORDER BY li.id_status_item;

PRINT '--- WebApplication metrics (MSK, test rows only) ---';
;WITH OpenToday AS (
    SELECT a.id_appointment
    FROM Appointment a
    WHERE a.date_arrival = @todayMsk AND a.id_client BETWEEN 99990001 AND 99990020
      AND a.time_complete IS NULL
),
CurrentStep AS (
    SELECT li.id_appointment, li.time_call, li.id_status_item, li.time_start_servicing,
        ROW_NUMBER() OVER (PARTITION BY li.id_appointment ORDER BY li.id_list_item ASC) AS rn
    FROM List_item li
    JOIN OpenToday o ON o.id_appointment = li.id_appointment
    WHERE li.time_end_servicing IS NULL
)
SELECT
    (SELECT COUNT(*)
     FROM CurrentStep cs
     WHERE cs.rn = 1 AND cs.time_call IS NULL AND cs.id_status_item = 1) AS waiting_tickets,
    (SELECT COUNT(*)
     FROM CurrentStep cs
     WHERE cs.rn = 1 AND cs.time_start_servicing IS NOT NULL) AS in_service_tickets,
    (SELECT COUNT(*)
     FROM Appointment a
     WHERE a.date_arrival = @todayMsk AND a.id_client BETWEEN 99990001 AND 99990020
       AND (
           a.time_complete IS NOT NULL
           OR (
               EXISTS (SELECT 1 FROM List_item li WHERE li.id_appointment = a.id_appointment)
               AND NOT EXISTS (
                   SELECT 1 FROM List_item li
                   WHERE li.id_appointment = a.id_appointment AND li.time_end_servicing IS NULL)
           )
       )) AS serviced_patients,
    (SELECT COUNT(*)
     FROM List_item li
     JOIN Appointment a ON a.id_appointment = li.id_appointment
     WHERE a.date_arrival = @todayMsk AND a.id_client BETWEEN 99990001 AND 99990020
       AND li.time_call IS NOT NULL AND li.time_start_servicing IS NOT NULL
       AND li.time_end_servicing IS NOT NULL) AS completed_stages_for_avg;

PRINT '--- Completed stage durations (expect spread, at least one > 5 min) ---';
;WITH StageDur AS (
    SELECT
        a.number,
        li.id_list_item,
        DATEDIFF(minute,
            CAST(CAST(a.date_arrival AS datetime) + CAST(li.time_start_servicing AS datetime) AS datetime2),
            CAST(CAST(a.date_arrival AS datetime) + CAST(li.time_end_servicing AS datetime) AS datetime2)) AS service_minutes
    FROM List_item li
    JOIN Appointment a ON a.id_appointment = li.id_appointment
    WHERE a.date_arrival = @todayMsk
      AND a.id_client BETWEEN 99990001 AND 99990020
      AND li.time_start_servicing IS NOT NULL
      AND li.time_end_servicing IS NOT NULL
)
SELECT number, id_list_item, service_minutes
FROM StageDur
ORDER BY number, id_list_item;

SELECT
    MAX(service_minutes) AS max_service_minutes,
    CASE WHEN MAX(service_minutes) > 5 THEN N'OK' ELSE N'FAIL' END AS max_service_check
FROM (
    SELECT DATEDIFF(minute,
            CAST(CAST(a.date_arrival AS datetime) + CAST(li.time_start_servicing AS datetime) AS datetime2),
            CAST(CAST(a.date_arrival AS datetime) + CAST(li.time_end_servicing AS datetime) AS datetime2)) AS service_minutes
    FROM List_item li
    JOIN Appointment a ON a.id_appointment = li.id_appointment
    WHERE a.date_arrival = @todayMsk
      AND a.id_client BETWEEN 99990001 AND 99990020
      AND li.time_start_servicing IS NOT NULL
      AND li.time_end_servicing IS NOT NULL
) d;

PRINT '--- In-service: elapsed minutes per ticket (expect >= 1 for service tickets) ---';
;WITH InService AS (
    SELECT
        a.number,
        a.id_client,
        FLOOR(DATEDIFF(second,
            CAST(CAST(a.date_arrival AS datetime) + CAST(li.time_start_servicing AS datetime) AS datetime2),
            @nowMsk) / 60.0) AS elapsed_minutes
    FROM Appointment a
    JOIN List_item li ON li.id_appointment = a.id_appointment
    WHERE a.date_arrival = @todayMsk
      AND a.id_client BETWEEN 99990001 AND 99990020
      AND a.time_complete IS NULL
      AND li.time_end_servicing IS NULL
      AND li.time_start_servicing IS NOT NULL
)
SELECT number, elapsed_minutes FROM InService ORDER BY number;

SELECT
    CASE WHEN MIN(elapsed_minutes) >= 1 THEN N'OK' ELSE N'FAIL' END AS in_service_min_check,
    COUNT(DISTINCT elapsed_minutes) AS distinct_elapsed
FROM (
    SELECT FLOOR(DATEDIFF(second,
            CAST(CAST(a.date_arrival AS datetime) + CAST(li.time_start_servicing AS datetime) AS datetime2),
            @nowMsk) / 60.0) AS elapsed_minutes
    FROM Appointment a
    JOIN List_item li ON li.id_appointment = a.id_appointment
    WHERE a.date_arrival = @todayMsk
      AND a.id_client BETWEEN 99990001 AND 99990020
      AND a.time_complete IS NULL
      AND li.time_end_servicing IS NULL
      AND li.time_start_servicing IS NOT NULL
) x;

PRINT '--- Waiting list: wait minutes per open ticket (expect spread, >= 1) ---';
;WITH OpenToday AS (
    SELECT a.id_appointment, a.number, a.date_arrival, a.time_arrival, a.id_client
    FROM Appointment a
    WHERE a.date_arrival = @todayMsk AND a.id_client BETWEEN 99990001 AND 99990020
      AND a.time_complete IS NULL
),
CurrentStep AS (
    SELECT li.id_appointment, li.time_call, li.time_start_servicing,
        ROW_NUMBER() OVER (PARTITION BY li.id_appointment ORDER BY li.id_list_item ASC) AS rn
    FROM List_item li
    JOIN OpenToday o ON o.id_appointment = li.id_appointment
    WHERE li.time_end_servicing IS NULL
),
WaitCalc AS (
    SELECT
        o.number,
        o.id_client,
        FLOOR(DATEDIFF(second,
            CAST(CAST(o.date_arrival AS datetime) + CAST(COALESCE(cs.time_call, o.time_arrival) AS datetime) AS datetime2),
            @nowMsk) / 60.0) AS wait_minutes
    FROM OpenToday o
    JOIN CurrentStep cs ON cs.id_appointment = o.id_appointment AND cs.rn = 1
    WHERE cs.time_start_servicing IS NULL
)
SELECT number, wait_minutes FROM WaitCalc ORDER BY number;

SELECT
    CASE WHEN MIN(wait_minutes) >= 1 THEN N'OK' ELSE N'FAIL' END AS wait_min_check,
    CASE WHEN MAX(wait_minutes) <= 20 THEN N'OK' ELSE N'FAIL' END AS max_wait_check,
    MAX(wait_minutes) AS max_wait_minutes,
    CASE WHEN COUNT(DISTINCT wait_minutes) >= 4 THEN N'OK' ELSE N'FAIL' END AS distinct_wait_check
FROM (
    SELECT FLOOR(DATEDIFF(second,
            CAST(CAST(o.date_arrival AS datetime) + CAST(COALESCE(cs.time_call, o.time_arrival) AS datetime) AS datetime2),
            @nowMsk) / 60.0) AS wait_minutes
    FROM (
        SELECT a.id_appointment, a.date_arrival, a.time_arrival
        FROM Appointment a
        WHERE a.date_arrival = @todayMsk AND a.id_client BETWEEN 99990001 AND 99990020
          AND a.time_complete IS NULL
    ) o
    JOIN (
        SELECT li.id_appointment, li.time_call, li.time_start_servicing,
            ROW_NUMBER() OVER (PARTITION BY li.id_appointment ORDER BY li.id_list_item ASC) AS rn
        FROM List_item li
        JOIN Appointment a ON a.id_appointment = li.id_appointment
        WHERE a.date_arrival = @todayMsk AND a.id_client BETWEEN 99990001 AND 99990020
          AND a.time_complete IS NULL AND li.time_end_servicing IS NULL
    ) cs ON cs.id_appointment = o.id_appointment AND cs.rn = 1
    WHERE cs.time_start_servicing IS NULL
) w;

PRINT '--- Completed wait before call (expect max <= 20 min) ---';
;WITH CompletedStages AS (
    SELECT
        a.id_appointment,
        a.date_arrival,
        a.time_arrival,
        li.id_list_item,
        li.time_call,
        li.time_end_servicing,
        ROW_NUMBER() OVER (PARTITION BY li.id_appointment ORDER BY li.id_list_item) AS step_rn
    FROM List_item li
    JOIN Appointment a ON a.id_appointment = li.id_appointment
    WHERE a.date_arrival = @todayMsk
      AND a.id_client BETWEEN 99990001 AND 99990020
      AND li.time_call IS NOT NULL
      AND li.time_end_servicing IS NOT NULL
),
WaitBeforeCall AS (
    SELECT
        cs.id_appointment,
        cs.step_rn,
        CASE
            WHEN cs.step_rn = 1 THEN
                DATEDIFF(minute,
                    CAST(CAST(cs.date_arrival AS datetime) + CAST(cs.time_arrival AS datetime) AS datetime2),
                    CAST(CAST(cs.date_arrival AS datetime) + CAST(cs.time_call AS datetime) AS datetime2))
            ELSE
                DATEDIFF(minute,
                    CAST(CAST(prev.date_arrival AS datetime) + CAST(prev.time_end_servicing AS datetime) AS datetime2),
                    CAST(CAST(cs.date_arrival AS datetime) + CAST(cs.time_call AS datetime) AS datetime2))
        END AS wait_before_call_min
    FROM CompletedStages cs
    LEFT JOIN CompletedStages prev
        ON prev.id_appointment = cs.id_appointment AND prev.step_rn = cs.step_rn - 1
)
SELECT id_appointment, step_rn, wait_before_call_min
FROM WaitBeforeCall
ORDER BY id_appointment, step_rn;

;WITH CompletedStages AS (
    SELECT
        a.id_appointment,
        a.date_arrival,
        a.time_arrival,
        li.id_list_item,
        li.time_call,
        li.time_end_servicing,
        ROW_NUMBER() OVER (PARTITION BY li.id_appointment ORDER BY li.id_list_item) AS step_rn
    FROM List_item li
    JOIN Appointment a ON a.id_appointment = li.id_appointment
    WHERE a.date_arrival = @todayMsk
      AND a.id_client BETWEEN 99990001 AND 99990020
      AND li.time_call IS NOT NULL
      AND li.time_end_servicing IS NOT NULL
),
WaitBeforeCall AS (
    SELECT
        cs.id_appointment,
        cs.step_rn,
        CASE
            WHEN cs.step_rn = 1 THEN
                DATEDIFF(minute,
                    CAST(CAST(cs.date_arrival AS datetime) + CAST(cs.time_arrival AS datetime) AS datetime2),
                    CAST(CAST(cs.date_arrival AS datetime) + CAST(cs.time_call AS datetime) AS datetime2))
            ELSE
                DATEDIFF(minute,
                    CAST(CAST(prev.date_arrival AS datetime) + CAST(prev.time_end_servicing AS datetime) AS datetime2),
                    CAST(CAST(cs.date_arrival AS datetime) + CAST(cs.time_call AS datetime) AS datetime2))
        END AS wait_before_call_min
    FROM CompletedStages cs
    LEFT JOIN CompletedStages prev
        ON prev.id_appointment = cs.id_appointment AND prev.step_rn = cs.step_rn - 1
)
SELECT
    CASE WHEN MAX(wait_before_call_min) <= 20 THEN N'OK' ELSE N'FAIL' END AS max_completed_wait_check,
    MAX(wait_before_call_min) AS max_completed_wait_min
FROM WaitBeforeCall;

PRINT '--- Waiting list (MSK, waiting + called, same as activeQueue) ---';
;WITH OpenToday AS (
    SELECT a.id_appointment, a.number, a.time_arrival
    FROM Appointment a
    WHERE a.date_arrival = @todayMsk AND a.id_client BETWEEN 99990001 AND 99990020
      AND a.time_complete IS NULL
),
CurrentStep AS (
    SELECT li.id_appointment, li.id_status_item, li.time_call, li.id_cabinet, li.time_start_servicing,
        ROW_NUMBER() OVER (PARTITION BY li.id_appointment ORDER BY li.id_list_item ASC) AS rn
    FROM List_item li
    JOIN OpenToday o ON o.id_appointment = li.id_appointment
    WHERE li.time_end_servicing IS NULL
)
SELECT o.number, cs.id_status_item, cs.time_call, cs.id_cabinet
FROM OpenToday o
JOIN CurrentStep cs ON cs.id_appointment = o.id_appointment AND cs.rn = 1
WHERE cs.time_start_servicing IS NULL
ORDER BY o.time_arrival;

PRINT '--- Summary checks (20-ticket seed) ---';
DECLARE @clientIdMin int = 99990001;
DECLARE @clientIdMax int = 99990020;

SELECT
    (SELECT COUNT(*) FROM Appointment a
     WHERE a.id_client BETWEEN @clientIdMin AND @clientIdMax AND a.date_arrival = @todayMsk) AS tickets_today,
    CASE WHEN (SELECT COUNT(*) FROM Appointment a
               WHERE a.id_client BETWEEN @clientIdMin AND @clientIdMax AND a.date_arrival = @todayMsk) = 20
         THEN N'OK' ELSE N'FAIL' END AS tickets_today_check;

;WITH OpenToday AS (
    SELECT a.id_appointment
    FROM Appointment a
    WHERE a.date_arrival = @todayMsk AND a.id_client BETWEEN @clientIdMin AND @clientIdMax
      AND a.time_complete IS NULL
),
CurrentStep AS (
    SELECT li.id_appointment, li.time_call, li.id_status_item, li.time_start_servicing,
        ROW_NUMBER() OVER (PARTITION BY li.id_appointment ORDER BY li.id_list_item ASC) AS rn
    FROM List_item li
    JOIN OpenToday o ON o.id_appointment = li.id_appointment
    WHERE li.time_end_servicing IS NULL
),
WaitingList AS (
    SELECT cs.id_appointment
    FROM CurrentStep cs
    WHERE cs.rn = 1
      AND cs.time_start_servicing IS NULL
      AND (
          cs.time_call IS NOT NULL
          OR cs.id_status_item IN (1, 5)
      )
)
SELECT
    (SELECT COUNT(*) FROM WaitingList) AS waiting_tickets,
    (SELECT COUNT(*)
     FROM CurrentStep cs
     WHERE cs.rn = 1 AND cs.time_start_servicing IS NOT NULL) AS in_service_tickets,
    CASE WHEN (SELECT COUNT(*) FROM WaitingList) >= 11
         THEN N'OK' ELSE N'FAIL' END AS waiting_check,
    CASE WHEN (SELECT COUNT(*)
               FROM CurrentStep cs
               WHERE cs.rn = 1 AND cs.time_start_servicing IS NOT NULL) >= 6
         THEN N'OK' ELSE N'FAIL' END AS in_service_check;

PRINT '--- Route patients (doctor modal: ticket, category, priority, waiting, call) ---';
;WITH OpenToday AS (
    SELECT a.id_appointment, a.number, a.id_category, a.date_arrival, a.time_arrival
    FROM Appointment a
    WHERE a.date_arrival = @todayMsk AND a.id_client BETWEEN @clientIdMin AND @clientIdMax
      AND a.time_complete IS NULL
),
CurrentStep AS (
    SELECT li.id_appointment, li.id_doctor, li.time_call, li.time_start_servicing, li.id_status_item,
        ROW_NUMBER() OVER (PARTITION BY li.id_appointment ORDER BY li.id_list_item ASC) AS rn
    FROM List_item li
    JOIN OpenToday o ON o.id_appointment = li.id_appointment
    WHERE li.time_end_servicing IS NULL
),
PotentialPatients AS (
    SELECT
        cs.id_doctor,
        o.number,
        cat.name AS category_name,
        cat.priority AS category_priority,
        DATEDIFF(
            minute,
            CASE
                WHEN cs.time_call IS NOT NULL THEN
                    CAST(CAST(o.date_arrival AS datetime) + CAST(cs.time_call AS datetime) AS datetime2)
                ELSE
                    CAST(CAST(o.date_arrival AS datetime) + CAST(o.time_arrival AS datetime) AS datetime2)
            END,
            SYSDATETIME()) AS waiting_min,
        CASE WHEN cs.time_call IS NOT NULL THEN N'Вызван' ELSE N'Не вызван' END AS call_label
    FROM OpenToday o
    JOIN CurrentStep cs ON cs.id_appointment = o.id_appointment AND cs.rn = 1
    JOIN Category cat ON cat.id_category = o.id_category
    WHERE cs.id_doctor IS NOT NULL
      AND cs.time_start_servicing IS NULL
      AND (
          cs.time_call IS NOT NULL
          OR cs.id_status_item IN (1, 5)
      )
)
SELECT id_doctor, number, category_name, category_priority, waiting_min, call_label
FROM PotentialPatients
ORDER BY id_doctor, category_priority DESC, number;

;WITH OpenToday AS (
    SELECT a.id_appointment, a.number, a.id_category, a.date_arrival, a.time_arrival
    FROM Appointment a
    WHERE a.date_arrival = @todayMsk AND a.id_client BETWEEN @clientIdMin AND @clientIdMax
      AND a.time_complete IS NULL
),
CurrentStep AS (
    SELECT li.id_appointment, li.id_doctor, li.time_call, li.time_start_servicing, li.id_status_item,
        ROW_NUMBER() OVER (PARTITION BY li.id_appointment ORDER BY li.id_list_item ASC) AS rn
    FROM List_item li
    JOIN OpenToday o ON o.id_appointment = li.id_appointment
    WHERE li.time_end_servicing IS NULL
),
PotentialPatients AS (
    SELECT
        cs.id_doctor,
        o.number,
        cat.name AS category_name,
        cat.priority AS category_priority,
        DATEDIFF(
            minute,
            CASE
                WHEN cs.time_call IS NOT NULL THEN
                    CAST(CAST(o.date_arrival AS datetime) + CAST(cs.time_call AS datetime) AS datetime2)
                ELSE
                    CAST(CAST(o.date_arrival AS datetime) + CAST(o.time_arrival AS datetime) AS datetime2)
            END,
            SYSDATETIME()) AS waiting_min,
        CASE WHEN cs.time_call IS NOT NULL THEN N'Вызван' ELSE N'Не вызван' END AS call_label
    FROM OpenToday o
    JOIN CurrentStep cs ON cs.id_appointment = o.id_appointment AND cs.rn = 1
    JOIN Category cat ON cat.id_category = o.id_category
    WHERE cs.id_doctor IS NOT NULL
      AND cs.time_start_servicing IS NULL
      AND (
          cs.time_call IS NOT NULL
          OR cs.id_status_item IN (1, 5)
      )
)
SELECT
    CASE WHEN EXISTS (
        SELECT 1 FROM PotentialPatients pp
        WHERE pp.category_name IS NULL OR LTRIM(RTRIM(pp.category_name)) = N''
    ) THEN N'FAIL' ELSE N'OK' END AS category_name_check,
    CASE WHEN EXISTS (SELECT 1 FROM PotentialPatients WHERE call_label = N'Вызван')
          AND EXISTS (SELECT 1 FROM PotentialPatients WHERE call_label = N'Не вызван')
         THEN N'OK' ELSE N'FAIL' END AS call_variety_check,
    CASE WHEN EXISTS (
        SELECT pp.id_doctor
        FROM PotentialPatients pp
        GROUP BY pp.id_doctor
        HAVING COUNT(*) >= 2
    ) THEN N'OK' ELSE N'FAIL' END AS multi_patient_doctor_check;

SELECT
    (SELECT COUNT(*)
     FROM Appointment a
     WHERE a.date_arrival = @todayMsk AND a.id_client BETWEEN @clientIdMin AND @clientIdMax
       AND (
           a.time_complete IS NOT NULL
           OR (
               EXISTS (SELECT 1 FROM List_item li WHERE li.id_appointment = a.id_appointment)
               AND NOT EXISTS (
                   SELECT 1 FROM List_item li
                   WHERE li.id_appointment = a.id_appointment AND li.time_end_servicing IS NULL)
           )
       )) AS serviced_patients,
    CASE WHEN (SELECT COUNT(*)
               FROM Appointment a
               WHERE a.date_arrival = @todayMsk AND a.id_client BETWEEN @clientIdMin AND @clientIdMax
                 AND (
                     a.time_complete IS NOT NULL
                     OR (
                         EXISTS (SELECT 1 FROM List_item li WHERE li.id_appointment = a.id_appointment)
                         AND NOT EXISTS (
                             SELECT 1 FROM List_item li
                             WHERE li.id_appointment = a.id_appointment AND li.time_end_servicing IS NULL)
                     )
                 )) >= 3
         THEN N'OK' ELSE N'FAIL' END AS serviced_check;

SELECT
    COUNT(*) AS open_log_work_for_test_doctors,
    CASE WHEN COUNT(*) >= 8 THEN N'OK' ELSE N'FAIL' END AS log_work_check
FROM Log_work lw
WHERE lw.date_work = @todayMsk
  AND lw.time_begin IS NOT NULL
  AND lw.time_end IS NULL
  AND EXISTS (
      SELECT 1
      FROM List_item li
      JOIN Appointment a ON a.id_appointment = li.id_appointment
      WHERE a.id_client BETWEEN @clientIdMin AND @clientIdMax
        AND a.date_arrival = @todayMsk
        AND li.id_doctor = lw.id_doctor
  );

PRINT 'Expected: tickets_today=20, waiting>=11, in_service>=6, serviced>=3, log_work>=8, distinct_wait>=4, max_wait<=20, potential patients checks OK.';

PRINT '--- No procedural-cabinet specialty (id 25) on test tickets ---';
SELECT a.number, d.full_name, s.definition
FROM Appointment a
JOIN List_item li ON li.id_appointment = a.id_appointment
JOIN Doctor d ON d.id_doctor = li.id_doctor
JOIN Specialty s ON s.id_specialty = li.id_specialty
WHERE a.id_client BETWEEN 99990001 AND 99990020
  AND a.date_arrival = @todayMsk
  AND li.id_specialty = 25;
IF @@ROWCOUNT = 0
    PRINT 'OK: no placeholder doctors on test tickets.';
ELSE
    PRINT 'WARN: procedural cabinet (id_specialty 25) still present on test route.';
