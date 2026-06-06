-- Preflight before dashboard-test-seed.sql (20 tickets, multiple source dates)
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

PRINT '--- Source tickets (required 20) ---';
SELECT s.ticket_rn, s.source_id, s.source_date, s.scenario, a.number AS source_number,
       (SELECT COUNT(*) FROM List_item li WHERE li.id_appointment = s.source_id) AS source_steps,
       CASE WHEN a.id_appointment IS NULL THEN 0 ELSE 1 END AS found
FROM (VALUES
    (1, 239062, CAST('2026-05-06' AS date), N'wait'),
    (2, 239064, CAST('2026-05-06' AS date), N'wait'),
    (3, 239065, CAST('2026-05-06' AS date), N'called'),
    (4, 239068, CAST('2026-05-06' AS date), N'called'),
    (5, 239070, CAST('2026-05-06' AS date), N'service'),
    (6, 239082, CAST('2026-05-06' AS date), N'service'),
    (7, 239084, CAST('2026-05-06' AS date), N'done'),
    (8, 239086, CAST('2026-05-06' AS date), N'results'),
    (9, 238835, CAST('2026-05-04' AS date), N'edge_single'),
    (10, 238926, CAST('2026-05-05' AS date), N'edge_pause'),
    (11, 238815, CAST('2026-05-04' AS date), N'wait'),
    (12, 238945, CAST('2026-05-05' AS date), N'wait'),
    (13, 239305, CAST('2026-05-08' AS date), N'wait'),
    (14, 238961, CAST('2026-05-05' AS date), N'service'),
    (15, 239213, CAST('2026-05-07' AS date), N'service'),
    (16, 239088, CAST('2026-05-06' AS date), N'service'),
    (17, 239220, CAST('2026-05-07' AS date), N'service'),
    (18, 238817, CAST('2026-05-04' AS date), N'done'),
    (19, 239096, CAST('2026-05-06' AS date), N'done'),
    (20, 239235, CAST('2026-05-07' AS date), N'results')
) s(ticket_rn, source_id, source_date, scenario)
LEFT JOIN Appointment a ON a.id_appointment = s.source_id AND a.date_arrival = s.source_date
ORDER BY s.ticket_rn;

IF EXISTS (
    SELECT 1
    FROM (VALUES
        (239062, CAST('2026-05-06' AS date)), (239064, CAST('2026-05-06' AS date)),
        (239065, CAST('2026-05-06' AS date)), (239068, CAST('2026-05-06' AS date)),
        (239070, CAST('2026-05-06' AS date)), (239082, CAST('2026-05-06' AS date)),
        (239084, CAST('2026-05-06' AS date)), (239086, CAST('2026-05-06' AS date)),
        (238835, CAST('2026-05-04' AS date)), (238926, CAST('2026-05-05' AS date)),
        (238815, CAST('2026-05-04' AS date)), (238945, CAST('2026-05-05' AS date)),
        (239305, CAST('2026-05-08' AS date)), (238961, CAST('2026-05-05' AS date)),
        (239213, CAST('2026-05-07' AS date)), (239088, CAST('2026-05-06' AS date)),
        (239220, CAST('2026-05-07' AS date)), (238817, CAST('2026-05-04' AS date)),
        (239096, CAST('2026-05-06' AS date)), (239235, CAST('2026-05-07' AS date))
    ) s(source_id, source_date)
    LEFT JOIN Appointment a ON a.id_appointment = s.source_id AND a.date_arrival = s.source_date
    WHERE a.id_appointment IS NULL
)
BEGIN
    PRINT '--- Replacement candidates (2026-05-03..2026-05-09, 1..3 steps) ---';
    SELECT TOP 30 a.date_arrival, a.id_appointment, COUNT(li.id_list_item) AS steps
    FROM Appointment a
    JOIN List_item li ON li.id_appointment = a.id_appointment
    WHERE a.date_arrival BETWEEN '2026-05-03' AND '2026-05-09'
    GROUP BY a.date_arrival, a.id_appointment
    HAVING COUNT(li.id_list_item) BETWEEN 1 AND 3
    ORDER BY a.date_arrival, steps DESC, a.id_appointment;
END;

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

PRINT '--- Next Numbers ---';
SELECT ISNULL(MAX(Number), 0) AS current_max_number FROM Numbers;

PRINT '--- Existing test rows ---';
SELECT COUNT(*) AS test_by_client_id FROM Appointment WHERE id_client BETWEEN @clientIdMin AND @clientIdMax;
