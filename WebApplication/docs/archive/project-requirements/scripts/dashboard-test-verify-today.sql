-- Verify seed: clone from 2026-05-06, today MSK (same as QueueDashboardClock)
SET NOCOUNT ON;

DECLARE @nowMsk datetime2 = SYSDATETIME();
DECLARE @todayMsk date = CAST(@nowMsk AS date);

PRINT '=== Verify ===';
PRINT 'Today (MSK): ' + CONVERT(varchar(10), @todayMsk, 120);
PRINT 'Now (MSK):   ' + CONVERT(varchar(30), @nowMsk, 120);

PRINT '--- Configurator: 8 tickets today, route_steps <= 3 ---';
SELECT a.number, a.id_status_app, a.info,
       COUNT(li.id_list_item) AS route_steps,
       MAX(CASE WHEN r.id_specialty IS NOT NULL THEN 1 ELSE 0 END) AS has_refer_step
FROM Appointment a
JOIN List_item li ON li.id_appointment = a.id_appointment
LEFT JOIN Refer r ON r.id_specialty = li.id_specialty AND r.id_cabinet = li.id_cabinet
WHERE a.id_client BETWEEN 99990001 AND 99990008
  AND a.date_arrival = @todayMsk
GROUP BY a.number, a.id_status_app, a.info
ORDER BY a.number;

SELECT COUNT(*) AS tickets_today FROM Appointment
WHERE id_client BETWEEN 99990001 AND 99990008 AND date_arrival = @todayMsk;

PRINT '--- Status coverage (appointment) ---';
SELECT a.id_status_app, COUNT(*) AS cnt
FROM Appointment a
WHERE a.id_client BETWEEN 99990001 AND 99990008 AND a.date_arrival = @todayMsk
GROUP BY a.id_status_app ORDER BY a.id_status_app;

PRINT '--- Status coverage (list_item current/max) ---';
SELECT li.id_status_item, COUNT(*) AS cnt
FROM List_item li
JOIN Appointment a ON a.id_appointment = li.id_appointment
WHERE a.id_client BETWEEN 99990001 AND 99990008 AND a.date_arrival = @todayMsk
GROUP BY li.id_status_item ORDER BY li.id_status_item;

PRINT '--- WebApplication metrics (MSK, test rows only) ---';
;WITH OpenToday AS (
    SELECT a.id_appointment
    FROM Appointment a
    WHERE a.date_arrival = @todayMsk AND a.id_client BETWEEN 99990001 AND 99990008
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
     WHERE a.date_arrival = @todayMsk AND a.id_client BETWEEN 99990001 AND 99990008
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
     WHERE a.date_arrival = @todayMsk AND a.id_client BETWEEN 99990001 AND 99990008
       AND li.time_call IS NOT NULL AND li.time_start_servicing IS NOT NULL
       AND li.time_end_servicing IS NOT NULL) AS completed_stages_for_avg;

PRINT '--- In-service: elapsed 0 min (time_start = now MSK) ---';
SELECT a.number, li.time_start_servicing,
    DATEDIFF(second,
        CAST(CAST(a.date_arrival AS datetime) + CAST(li.time_start_servicing AS datetime) AS datetime2),
        @nowMsk) AS elapsed_seconds,
    CASE WHEN DATEDIFF(second,
            CAST(CAST(a.date_arrival AS datetime) + CAST(li.time_start_servicing AS datetime) AS datetime2),
            @nowMsk) <= 0 THEN 0
         ELSE FLOOR(DATEDIFF(second,
            CAST(CAST(a.date_arrival AS datetime) + CAST(li.time_start_servicing AS datetime) AS datetime2),
            @nowMsk) / 60.0) END AS elapsed_minutes
FROM Appointment a
JOIN List_item li ON li.id_appointment = a.id_appointment
WHERE a.date_arrival = @todayMsk
  AND a.id_client BETWEEN 99990001 AND 99990008
  AND a.time_complete IS NULL
  AND li.time_end_servicing IS NULL
  AND li.time_start_servicing IS NOT NULL
ORDER BY a.number, li.id_list_item;

PRINT '--- Waiting list: wait minutes 0 (time_call or time_arrival = now) ---';
;WITH OpenToday AS (
    SELECT a.id_appointment, a.number, a.date_arrival, a.time_arrival
    FROM Appointment a
    WHERE a.date_arrival = @todayMsk AND a.id_client BETWEEN 99990001 AND 99990008
      AND a.time_complete IS NULL
),
CurrentStep AS (
    SELECT li.id_appointment, li.time_call, li.time_start_servicing,
        ROW_NUMBER() OVER (PARTITION BY li.id_appointment ORDER BY li.id_list_item ASC) AS rn
    FROM List_item li
    JOIN OpenToday o ON o.id_appointment = li.id_appointment
    WHERE li.time_end_servicing IS NULL
)
SELECT o.number,
    DATEDIFF(second,
        CAST(CAST(o.date_arrival AS datetime) + CAST(COALESCE(cs.time_call, o.time_arrival) AS datetime) AS datetime2),
        @nowMsk) AS wait_elapsed_seconds,
    CASE WHEN DATEDIFF(second,
            CAST(CAST(o.date_arrival AS datetime) + CAST(COALESCE(cs.time_call, o.time_arrival) AS datetime) AS datetime2),
            @nowMsk) <= 0 THEN 0
         ELSE FLOOR(DATEDIFF(second,
            CAST(CAST(o.date_arrival AS datetime) + CAST(COALESCE(cs.time_call, o.time_arrival) AS datetime) AS datetime2),
            @nowMsk) / 60.0) END AS wait_minutes
FROM OpenToday o
JOIN CurrentStep cs ON cs.id_appointment = o.id_appointment AND cs.rn = 1
WHERE cs.time_start_servicing IS NULL
ORDER BY o.number;

PRINT '--- Waiting list (MSK, waiting + called, same as activeQueue) ---';
;WITH OpenToday AS (
    SELECT a.id_appointment, a.number, a.time_arrival
    FROM Appointment a
    WHERE a.date_arrival = @todayMsk AND a.id_client BETWEEN 99990001 AND 99990008
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

PRINT 'Expected: wait_minutes = 0 and elapsed_minutes = 0 when elapsed_seconds is 0..59 (open test tickets 3-6 on dashboard).';

PRINT '--- No procedural-cabinet specialty (id 25) on test tickets ---';
SELECT a.number, d.full_name, s.definition
FROM Appointment a
JOIN List_item li ON li.id_appointment = a.id_appointment
JOIN Doctor d ON d.id_doctor = li.id_doctor
JOIN Specialty s ON s.id_specialty = li.id_specialty
WHERE a.id_client BETWEEN 99990001 AND 99990008
  AND a.date_arrival = @todayMsk
  AND li.id_specialty = 25;
IF @@ROWCOUNT = 0
    PRINT 'OK: no placeholder doctors on test tickets.';
ELSE
    PRINT 'WARN: procedural cabinet (id_specialty 25) still present on test route.';
