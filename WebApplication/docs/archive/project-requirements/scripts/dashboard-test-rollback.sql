-- Remove dashboard test data
-- Marker: id_client 99990001..99990008, info = N'-'; legacy DASHBOARD_TEST / D-T%
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

DELETE li
FROM List_item li
INNER JOIN Appointment a ON a.id_appointment = li.id_appointment
WHERE a.id_client BETWEEN 99990001 AND 99990008
   OR a.info LIKE N'%DASHBOARD_TEST%'
   OR a.number LIKE N'D-T%';

DELETE FROM Appointment
WHERE id_client BETWEEN 99990001 AND 99990008
   OR info LIKE N'%DASHBOARD_TEST%'
   OR number LIKE N'D-T%';

COMMIT;

SELECT COUNT(*) AS remaining_test_appointments
FROM Appointment
WHERE id_client BETWEEN 99990001 AND 99990008
   OR info LIKE N'%DASHBOARD_TEST%'
   OR number LIKE N'D-T%';

SELECT COUNT(*) AS test_on_today_msk
FROM Appointment
WHERE id_client BETWEEN 99990001 AND 99990008
  AND date_arrival = CAST(SYSDATETIME() AS date);
