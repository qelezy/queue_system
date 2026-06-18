SET NOCOUNT ON;

DECLARE @todayMsk date = CAST(SYSDATETIME() AS date);
DECLARE @clientIdMin int = 99990001;
DECLARE @clientIdMax int = 99990020;

PRINT '=== Dashboard test preflight (20 tickets) ===';
PRINT 'Today MSK (/dashboard): ' + CONVERT(varchar(10), @todayMsk, 120);

IF EXISTS (
    SELECT 1 FROM Appointment
    WHERE id_client BETWEEN @clientIdMin AND @clientIdMax
       OR info LIKE N'%DASHBOARD_TEST%'
       OR number LIKE N'D-T%'
)
    PRINT 'WARNING: Test rows exist. Run dashboard-test-rollback.sql first.';

PRINT '--- Dynamic source route candidates ---';
;WITH RouteCandidates AS (
    SELECT a.id_appointment, a.date_arrival, COUNT(*) AS route_steps
    FROM Appointment a
    INNER JOIN Category cat ON cat.id_category = a.id_category
    INNER JOIN List_item li ON li.id_appointment = a.id_appointment
    LEFT JOIN Doctor d ON d.id_doctor = li.id_doctor
    LEFT JOIN Specialty sp ON sp.id_specialty = li.id_specialty
    LEFT JOIN Cabinet cab ON cab.id_cabinet = li.id_cabinet
    LEFT JOIN Refer r ON r.id_specialty = li.id_specialty AND r.id_cabinet = li.id_cabinet
    WHERE (a.id_client IS NULL OR a.id_client NOT BETWEEN @clientIdMin AND @clientIdMax)
      AND NULLIF(LTRIM(RTRIM(a.number)), '') IS NOT NULL
      AND NULLIF(LTRIM(RTRIM(cat.name)), '') IS NOT NULL
      AND NULLIF(LTRIM(RTRIM(cat.letter)), '') IS NOT NULL
    GROUP BY a.id_appointment, a.date_arrival
    HAVING COUNT(*) BETWEEN 1 AND 3
       AND SUM(CASE
           WHEN li.id_doctor IS NULL OR li.id_doctor <= 0
             OR li.id_cabinet IS NULL OR li.id_cabinet <= 0
             OR li.id_specialty = 25
             OR NULLIF(LTRIM(RTRIM(d.full_name)), '') IS NULL
             OR NULLIF(LTRIM(RTRIM(sp.definition)), '') IS NULL
             OR NULLIF(LTRIM(RTRIM(cab.cabinet_number)), '') IS NULL
             OR r.id_specialty IS NULL
           THEN 1 ELSE 0 END) = 0
)
SELECT route_steps, COUNT(*) AS valid_routes
FROM RouteCandidates
GROUP BY route_steps
ORDER BY route_steps;

PRINT '--- Dynamic source route sample ---';
;WITH RouteCandidates AS (
    SELECT a.id_appointment, a.date_arrival, a.number, COUNT(*) AS route_steps
    FROM Appointment a
    INNER JOIN Category cat ON cat.id_category = a.id_category
    INNER JOIN List_item li ON li.id_appointment = a.id_appointment
    LEFT JOIN Doctor d ON d.id_doctor = li.id_doctor
    LEFT JOIN Specialty sp ON sp.id_specialty = li.id_specialty
    LEFT JOIN Cabinet cab ON cab.id_cabinet = li.id_cabinet
    LEFT JOIN Refer r ON r.id_specialty = li.id_specialty AND r.id_cabinet = li.id_cabinet
    WHERE (a.id_client IS NULL OR a.id_client NOT BETWEEN @clientIdMin AND @clientIdMax)
      AND NULLIF(LTRIM(RTRIM(a.number)), '') IS NOT NULL
      AND NULLIF(LTRIM(RTRIM(cat.name)), '') IS NOT NULL
      AND NULLIF(LTRIM(RTRIM(cat.letter)), '') IS NOT NULL
    GROUP BY a.id_appointment, a.date_arrival, a.number
    HAVING COUNT(*) BETWEEN 1 AND 3
       AND SUM(CASE
           WHEN li.id_doctor IS NULL OR li.id_doctor <= 0
             OR li.id_cabinet IS NULL OR li.id_cabinet <= 0
             OR li.id_specialty = 25
             OR NULLIF(LTRIM(RTRIM(d.full_name)), '') IS NULL
             OR NULLIF(LTRIM(RTRIM(sp.definition)), '') IS NULL
             OR NULLIF(LTRIM(RTRIM(cab.cabinet_number)), '') IS NULL
             OR r.id_specialty IS NULL
           THEN 1 ELSE 0 END) = 0
)
SELECT TOP 20 id_appointment, date_arrival, number, route_steps
FROM RouteCandidates
ORDER BY date_arrival DESC, id_appointment DESC;

PRINT '--- Dynamic doctor references ---';
;WITH PairUsage AS (
    SELECT li.id_doctor, li.id_specialty, li.id_cabinet, COUNT(*) AS cnt
    FROM List_item li
    INNER JOIN Doctor d ON d.id_doctor = li.id_doctor
    INNER JOIN Specialty sp ON sp.id_specialty = li.id_specialty
    INNER JOIN Cabinet cab ON cab.id_cabinet = li.id_cabinet
    INNER JOIN Refer r ON r.id_specialty = li.id_specialty AND r.id_cabinet = li.id_cabinet
    WHERE li.id_doctor > 0
      AND li.id_cabinet > 0
      AND li.id_specialty <> 25
      AND NULLIF(LTRIM(RTRIM(d.full_name)), '') IS NOT NULL
      AND NULLIF(LTRIM(RTRIM(sp.definition)), '') IS NOT NULL
      AND NULLIF(LTRIM(RTRIM(cab.cabinet_number)), '') IS NOT NULL
    GROUP BY li.id_doctor, li.id_specialty, li.id_cabinet
),
RankedPairs AS (
    SELECT id_doctor, id_specialty, id_cabinet, cnt,
           ROW_NUMBER() OVER (PARTITION BY id_doctor ORDER BY cnt DESC, id_specialty, id_cabinet) AS rn
    FROM PairUsage
)
SELECT COUNT(*) AS doctors_with_valid_primary_pair,
       CASE WHEN COUNT(*) >= 12 THEN N'OK' ELSE N'FAIL' END AS doctor_reference_check
FROM RankedPairs
WHERE rn = 1;

;WITH PairUsage AS (
    SELECT li.id_doctor, li.id_specialty, li.id_cabinet, COUNT(*) AS cnt
    FROM List_item li
    INNER JOIN Doctor d ON d.id_doctor = li.id_doctor
    INNER JOIN Specialty sp ON sp.id_specialty = li.id_specialty
    INNER JOIN Cabinet cab ON cab.id_cabinet = li.id_cabinet
    INNER JOIN Refer r ON r.id_specialty = li.id_specialty AND r.id_cabinet = li.id_cabinet
    WHERE li.id_doctor > 0
      AND li.id_cabinet > 0
      AND li.id_specialty <> 25
      AND NULLIF(LTRIM(RTRIM(d.full_name)), '') IS NOT NULL
      AND NULLIF(LTRIM(RTRIM(sp.definition)), '') IS NOT NULL
      AND NULLIF(LTRIM(RTRIM(cab.cabinet_number)), '') IS NOT NULL
    GROUP BY li.id_doctor, li.id_specialty, li.id_cabinet
),
RankedPairs AS (
    SELECT id_doctor, id_specialty, id_cabinet, cnt,
           ROW_NUMBER() OVER (PARTITION BY id_doctor ORDER BY cnt DESC, id_specialty, id_cabinet) AS rn
    FROM PairUsage
)
SELECT TOP 12 id_doctor, id_specialty, id_cabinet, cnt
FROM RankedPairs
WHERE rn = 1
ORDER BY cnt DESC, id_doctor;

PRINT '--- Status_Appointment / Status_item_list (need ids 1..5) ---';
SELECT id_status_app, name FROM Status_Appointment ORDER BY id_status_app;
SELECT id_status_item, name FROM Status_item_list ORDER BY id_status_item;

PRINT '--- Planned scenarios ---';
SELECT scenario, COUNT(*) AS cnt FROM (VALUES
    (N'wait'), (N'wait'), (N'wait'), (N'wait'), (N'wait'),
    (N'called'), (N'called'),
    (N'service'), (N'service'), (N'service'), (N'service'), (N'service'), (N'service'),
    (N'done'), (N'done'), (N'done'),
    (N'results'), (N'results'),
    (N'edge_single'), (N'edge_pause')
) v(scenario)
GROUP BY scenario ORDER BY scenario;

PRINT '--- Source ticket numeric range ---';
;WITH RouteCandidates AS (
    SELECT TOP 20
        a.number,
        cat.letter,
        TRY_CONVERT(int, SUBSTRING(a.number, LEN(cat.letter) + 1, 31)) AS source_ticket_num
    FROM Appointment a
    INNER JOIN Category cat ON cat.id_category = a.id_category
    INNER JOIN List_item li ON li.id_appointment = a.id_appointment
    LEFT JOIN Doctor d ON d.id_doctor = li.id_doctor
    LEFT JOIN Specialty sp ON sp.id_specialty = li.id_specialty
    LEFT JOIN Cabinet cab ON cab.id_cabinet = li.id_cabinet
    LEFT JOIN Refer r ON r.id_specialty = li.id_specialty AND r.id_cabinet = li.id_cabinet
    WHERE (a.id_client IS NULL OR a.id_client NOT BETWEEN @clientIdMin AND @clientIdMax)
      AND NULLIF(LTRIM(RTRIM(a.number)), '') IS NOT NULL
      AND NULLIF(LTRIM(RTRIM(cat.name)), '') IS NOT NULL
      AND NULLIF(LTRIM(RTRIM(cat.letter)), '') IS NOT NULL
    GROUP BY a.id_appointment, a.date_arrival, a.number, cat.letter
    HAVING COUNT(*) BETWEEN 1 AND 3
       AND SUM(CASE
           WHEN li.id_doctor IS NULL OR li.id_doctor <= 0
             OR li.id_cabinet IS NULL OR li.id_cabinet <= 0
             OR li.id_specialty = 25
             OR NULLIF(LTRIM(RTRIM(d.full_name)), '') IS NULL
             OR NULLIF(LTRIM(RTRIM(sp.definition)), '') IS NULL
             OR NULLIF(LTRIM(RTRIM(cab.cabinet_number)), '') IS NULL
             OR r.id_specialty IS NULL
           THEN 1 ELSE 0 END) = 0
    ORDER BY a.date_arrival DESC, a.id_appointment DESC
)
SELECT MIN(source_ticket_num) AS min_source_ticket_num,
       MAX(source_ticket_num) AS max_source_ticket_num
FROM RouteCandidates;

PRINT '--- Existing test rows ---';
SELECT COUNT(*) AS test_by_client_id FROM Appointment WHERE id_client BETWEEN @clientIdMin AND @clientIdMax;
