SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @todayMsk date = CAST(SYSDATETIME() AS date);
DECLARE @clientIdMin int = 99990001;
DECLARE @clientIdMax int = 99990020;

DECLARE @testDoctors TABLE (id_doctor int NOT NULL PRIMARY KEY);

INSERT INTO @testDoctors (id_doctor)
SELECT DISTINCT li.id_doctor
FROM List_item li
INNER JOIN Appointment a ON a.id_appointment = li.id_appointment
WHERE (
        a.id_client BETWEEN @clientIdMin AND @clientIdMax
        OR a.info LIKE N'%DASHBOARD_TEST%'
        OR a.number LIKE N'D-T%'
    )
  AND li.id_doctor IS NOT NULL
  AND li.id_doctor > 0;

BEGIN TRAN;

DELETE li
FROM List_item li
INNER JOIN Appointment a ON a.id_appointment = li.id_appointment
WHERE a.id_client BETWEEN @clientIdMin AND @clientIdMax
   OR a.info LIKE N'%DASHBOARD_TEST%'
   OR a.number LIKE N'D-T%';

DELETE FROM Appointment
WHERE id_client BETWEEN @clientIdMin AND @clientIdMax
   OR info LIKE N'%DASHBOARD_TEST%'
   OR number LIKE N'D-T%';

DELETE lw
FROM Log_work lw
INNER JOIN @testDoctors td ON td.id_doctor = lw.id_doctor
WHERE lw.date_work = @todayMsk;

COMMIT;

SELECT COUNT(*) AS remaining_test_appointments
FROM Appointment
WHERE id_client BETWEEN @clientIdMin AND @clientIdMax
   OR info LIKE N'%DASHBOARD_TEST%'
   OR number LIKE N'D-T%';

SELECT COUNT(*) AS test_on_today_msk
FROM Appointment
WHERE id_client BETWEEN @clientIdMin AND @clientIdMax
  AND date_arrival = @todayMsk;

SELECT COUNT(*) AS open_log_work_for_test_doctors_today
FROM Log_work lw
INNER JOIN @testDoctors td ON td.id_doctor = lw.id_doctor
WHERE lw.date_work = @todayMsk
  AND lw.time_begin IS NOT NULL
  AND lw.time_end IS NULL;
