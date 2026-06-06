-- Remove dashboard test data
-- Marker: id_client 99990001..99990020, info = N'-'; legacy DASHBOARD_TEST / D-T%
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

INSERT INTO @testDoctors (id_doctor)
SELECT DISTINCT li.id_doctor
FROM (
    VALUES
        (239062), (239064), (239065), (239068), (239070), (239082), (239084), (239086),
        (238835), (238926), (238815), (238945), (239305), (238961), (239213), (239088),
        (239220), (238817), (239096), (239235)
) s(source_id)
INNER JOIN List_item li ON li.id_appointment = s.source_id
WHERE li.id_doctor IS NOT NULL
  AND li.id_doctor > 0
  AND li.id_list_item = (
      SELECT MIN(li2.id_list_item)
      FROM List_item li2
      WHERE li2.id_appointment = li.id_appointment
  )
  AND NOT EXISTS (SELECT 1 FROM @testDoctors td WHERE td.id_doctor = li.id_doctor);

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
