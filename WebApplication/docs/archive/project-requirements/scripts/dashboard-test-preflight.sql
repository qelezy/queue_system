-- Preflight before dashboard-test-seed.sql (clone from 2026-05-06)
SET NOCOUNT ON;

DECLARE @sourceDate date = '2026-05-06';
DECLARE @todayMsk date = CAST(SYSDATETIME() AS date);

PRINT '=== Dashboard test preflight ===';
PRINT 'Source date: ' + CONVERT(varchar(10), @sourceDate, 120);
PRINT 'Today MSK (/dashboard): ' + CONVERT(varchar(10), @todayMsk, 120);

IF EXISTS (
    SELECT 1 FROM Appointment
    WHERE id_client BETWEEN 99990001 AND 99990008
       OR info LIKE N'%DASHBOARD_TEST%'
       OR number LIKE N'D-T%'
)
    PRINT 'WARNING: Test rows exist. Run dashboard-test-rollback.sql first.';

PRINT '--- Source tickets 2026-05-06 (required 8) ---';
SELECT s.ticket_rn, s.source_id, a.number AS source_number, a.id_category,
       (SELECT COUNT(*) FROM List_item li WHERE li.id_appointment = s.source_id) AS source_steps,
       CASE WHEN a.id_appointment IS NULL THEN 0 ELSE 1 END AS found
FROM (VALUES
    (1, 239062), (2, 239064), (3, 239065), (4, 239068),
    (5, 239070), (6, 239082), (7, 239084), (8, 239086)
) s(ticket_rn, source_id)
LEFT JOIN Appointment a ON a.id_appointment = s.source_id AND a.date_arrival = @sourceDate
ORDER BY s.ticket_rn;

PRINT '--- Status_Appointment / Status_item_list (need ids 1..5) ---';
SELECT id_status_app, name FROM Status_Appointment ORDER BY id_status_app;
SELECT id_status_item, name FROM Status_item_list ORDER BY id_status_item;

PRINT '--- Planned scenarios (8 tickets, max 3 steps each) ---';
SELECT ticket_rn, scenario, id_status_app FROM (VALUES
    (1, N'wait', 1), (2, N'wait', 1), (3, N'called', 2), (4, N'called', 2),
    (5, N'service', 3), (6, N'service', 3), (7, N'done', 4), (8, N'results', 1)
) v(ticket_rn, scenario, id_status_app);

PRINT '--- Next Numbers ---';
SELECT ISNULL(MAX(Number), 0) AS current_max_number FROM Numbers;

PRINT '--- Existing test rows ---';
SELECT COUNT(*) AS test_by_client_id FROM Appointment WHERE id_client BETWEEN 99990001 AND 99990008;
