-- Purge ALL queue data for a calendar day (Appointment + List_item + Log_work).
-- Default: 2026-05-31. For "today" use: DECLARE @purgeDate date = CAST(SYSDATETIME() AS date);
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @purgeDate date = '2026-05-31';

PRINT '=== Purge date: ' + CONVERT(varchar(10), @purgeDate, 120) + ' ===';

SELECT COUNT(*) AS appointments_before
FROM Appointment
WHERE date_arrival = @purgeDate;

SELECT COUNT(*) AS list_items_before
FROM List_item li
INNER JOIN Appointment a ON a.id_appointment = li.id_appointment
WHERE a.date_arrival = @purgeDate;

SELECT COUNT(*) AS log_work_before
FROM Log_work
WHERE date_work = @purgeDate;

BEGIN TRAN;

DELETE li
FROM List_item li
INNER JOIN Appointment a ON a.id_appointment = li.id_appointment
WHERE a.date_arrival = @purgeDate;

DELETE FROM Appointment
WHERE date_arrival = @purgeDate;

DELETE FROM Log_work
WHERE date_work = @purgeDate;

COMMIT;

SELECT COUNT(*) AS appointments_remaining
FROM Appointment
WHERE date_arrival = @purgeDate;

SELECT COUNT(*) AS list_items_remaining
FROM List_item li
INNER JOIN Appointment a ON a.id_appointment = li.id_appointment
WHERE a.date_arrival = @purgeDate;

SELECT COUNT(*) AS log_work_remaining
FROM Log_work
WHERE date_work = @purgeDate;

PRINT 'Purge complete.';
