-- Test data: clone 8 tickets from 2026-05-06 to today (Configurator + /dashboard).
-- Marker: info = N'-', id_client 99990001..99990008. Rollback: dashboard-test-rollback.sql
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @sourceDate date = '2026-05-06';
DECLARE @nowMsk datetime2 = SYSDATETIME();
DECLARE @todayMsk date = CAST(@nowMsk AS date);

IF EXISTS (
    SELECT 1 FROM Appointment
    WHERE id_client BETWEEN 99990001 AND 99990008
       OR info LIKE N'%DASHBOARD_TEST%'
       OR number LIKE N'D-T%'
)
BEGIN
    RAISERROR('Dashboard test rows already exist. Run dashboard-test-rollback.sql first.', 16, 1);
    RETURN;
END;

IF (SELECT COUNT(*) FROM Status_Appointment) < 5 OR (SELECT COUNT(*) FROM Status_item_list) < 5
BEGIN
    RAISERROR('Need Status_Appointment and Status_item_list with ids 1..5.', 16, 1);
    RETURN;
END;

DECLARE @st_wait int = 1, @st_called int = 2, @st_service int = 3, @st_done int = 4, @st_results int = 5;
DECLARE @sa_wait int = 1, @sa_called int = 2, @sa_service int = 3, @sa_done int = 4;

DECLARE @sources TABLE (
    ticket_rn int NOT NULL PRIMARY KEY,
    source_id int NOT NULL,
    id_status_app int NOT NULL,
    scenario nvarchar(20) NOT NULL
);

INSERT INTO @sources (ticket_rn, source_id, id_status_app, scenario) VALUES
    (1, 239062, @sa_wait, N'wait'),
    (2, 239064, @sa_wait, N'wait'),
    (3, 239065, @sa_called, N'called'),
    (4, 239068, @sa_called, N'called'),
    (5, 239070, @sa_service, N'service'),
    (6, 239082, @sa_service, N'service'),
    (7, 239084, @sa_done, N'done'),
    (8, 239086, @sa_wait, N'results');

DECLARE @ticketTiming TABLE (
    ticket_rn int NOT NULL PRIMARY KEY,
    arrival_min int NOT NULL,
    complete_min int NULL
);

INSERT INTO @ticketTiming (ticket_rn, arrival_min, complete_min) VALUES
    (1, 18, NULL),
    (2, 12, NULL),
    (3, 20, NULL),
    (4, 18, NULL),
    (5, 22, NULL),
    (6, 20, NULL),
    (7, 24, 3),
    (8, 16, NULL);

DECLARE @stepTiming TABLE (
    ticket_rn int NOT NULL,
    step_rn int NOT NULL,
    call_min int NULL,
    start_min int NULL,
    end_min int NULL,
    id_status_item int NOT NULL,
    result_received bit NOT NULL,
    PRIMARY KEY (ticket_rn, step_rn)
);

INSERT INTO @stepTiming (ticket_rn, step_rn, call_min, start_min, end_min, id_status_item, result_received) VALUES
    (1, 1, NULL, NULL, NULL, @st_wait, 0),
    (1, 2, NULL, NULL, NULL, @st_wait, 0),
    (1, 3, NULL, NULL, NULL, @st_wait, 0),
    (2, 1, NULL, NULL, NULL, @st_wait, 0),
    (2, 2, NULL, NULL, NULL, @st_wait, 0),
    (2, 3, NULL, NULL, NULL, @st_wait, 0),
    (3, 1, 18, 16, 9, @st_done, 1),
    (3, 2, 12, 10, 3, @st_done, 1),
    (3, 3, 9, NULL, NULL, @st_called, 0),
    (4, 1, 16, 14, 7, @st_done, 1),
    (4, 2, 10, 8, 1, @st_done, 1),
    (4, 3, 4, NULL, NULL, @st_called, 0),
    (5, 1, 20, 18, 11, @st_done, 1),
    (5, 2, 14, 12, 5, @st_done, 1),
    (5, 3, 14, 11, NULL, @st_service, 0),
    (6, 1, 18, 16, 9, @st_done, 1),
    (6, 2, 12, 10, 3, @st_done, 1),
    (6, 3, 8, 5, NULL, @st_service, 0),
    (7, 1, 20, 18, 11, @st_done, 1),
    (7, 2, 14, 12, 5, @st_done, 1),
    (7, 3, 10, 8, 1, @st_done, 1),
    (8, 1, 14, 12, 5, @st_done, 1),
    (8, 2, 8, 6, 1, @st_done, 1),
    (8, 3, NULL, NULL, NULL, @st_results, 0);

IF EXISTS (
    SELECT s.ticket_rn
    FROM @sources s
    LEFT JOIN Appointment a ON a.id_appointment = s.source_id AND a.date_arrival = @sourceDate
    WHERE a.id_appointment IS NULL
)
BEGIN
    RAISERROR('Source appointments for 2026-05-06 not found. Check ids 239062..239086.', 16, 1);
    RETURN;
END;

DECLARE @cloneRoute TABLE (
    ticket_rn int NOT NULL,
    step_rn int NOT NULL,
    id_category int NOT NULL,
    letter nchar(1) NOT NULL,
    priority int NOT NULL,
    id_specialty int NOT NULL,
    id_cabinet int NULL,
    id_doctor int NULL,
    PRIMARY KEY (ticket_rn, step_rn)
);

INSERT INTO @cloneRoute (ticket_rn, step_rn, id_category, letter, priority, id_specialty, id_cabinet, id_doctor)
SELECT
    s.ticket_rn,
    x.step_rn,
    a.id_category,
    cat.letter,
    a.priority,
    x.id_specialty,
    x.id_cabinet,
    x.id_doctor
FROM @sources s
INNER JOIN Appointment a ON a.id_appointment = s.source_id
INNER JOIN Category cat ON cat.id_category = a.id_category
CROSS APPLY (
    SELECT
        ROW_NUMBER() OVER (ORDER BY li.id_list_item) AS step_rn,
        li.id_specialty,
        li.id_cabinet,
        li.id_doctor
    FROM List_item li
    WHERE li.id_appointment = s.source_id
) x
WHERE x.step_rn <= 3;

IF (SELECT COUNT(DISTINCT ticket_rn) FROM @cloneRoute) < 8
BEGIN
    RAISERROR('Could not load 3-step routes for all 8 source tickets.', 16, 1);
    RETURN;
END;

DECLARE @spec_procedural int = 25;

UPDATE cr
SET
    cr.id_doctor = alt.id_doctor,
    cr.id_specialty = alt.id_specialty,
    cr.id_cabinet = alt.id_cabinet
FROM @cloneRoute cr
CROSS APPLY (
    SELECT li.id_doctor, li.id_specialty, li.id_cabinet
    FROM (
        SELECT
            li2.id_doctor,
            li2.id_specialty,
            li2.id_cabinet,
            ROW_NUMBER() OVER (ORDER BY li2.id_list_item) AS step_rn
        FROM List_item li2
        WHERE li2.id_appointment = 239065
          AND li2.id_specialty <> @spec_procedural
    ) li
    WHERE li.step_rn = 3
) alt
WHERE cr.ticket_rn = 5
  AND cr.step_rn = 3
  AND cr.id_specialty = @spec_procedural;

DECLARE @num_base int = (SELECT ISNULL(MAX(Number), 0) FROM Numbers);
DECLARE @id_client_test int = 99990000;

DECLARE @nums TABLE (
    ticket_rn int NOT NULL PRIMARY KEY,
    ticket_num int NOT NULL,
    letter nchar(1) NOT NULL
);

INSERT INTO @nums (ticket_rn, ticket_num, letter)
SELECT cr.ticket_rn, @num_base + cr.ticket_rn, cr.letter
FROM @cloneRoute cr
WHERE cr.step_rn = 1;

DECLARE @newAppt TABLE (ticket_rn int NOT NULL PRIMARY KEY, id_appointment int NOT NULL);

BEGIN TRAN;

SET IDENTITY_INSERT Numbers ON;
INSERT INTO Numbers (Number)
SELECT ticket_num FROM @nums;
SET IDENTITY_INSERT Numbers OFF;

INSERT INTO Log_work (id_cabinet, id_doctor, date_work, time_begin, time_end, last_refresh)
SELECT cr.id_cabinet, cr.id_doctor, @todayMsk, CAST('08:00' AS time), NULL, GETDATE()
FROM @cloneRoute cr
WHERE cr.step_rn = 1 AND cr.ticket_rn <= 3 AND cr.id_doctor IS NOT NULL AND cr.id_cabinet IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM Log_work lw
      WHERE lw.id_doctor = cr.id_doctor AND lw.date_work = @todayMsk
  );

DECLARE @tr int = 1;
DECLARE @aid int, @num nvarchar(32), @cat int, @pri int, @sap int;
DECLARE @arrivalMin int, @completeMin int;
DECLARE @tarrival time, @tcomplete time;

WHILE @tr <= 8
BEGIN
    SELECT
        @cat = cr.id_category,
        @pri = cr.priority,
        @sap = s.id_status_app,
        @num = n.letter + CAST(n.ticket_num AS nvarchar(31)),
        @arrivalMin = tt.arrival_min,
        @completeMin = tt.complete_min
    FROM @sources s
    INNER JOIN @nums n ON n.ticket_rn = s.ticket_rn
    INNER JOIN @cloneRoute cr ON cr.ticket_rn = s.ticket_rn AND cr.step_rn = 1
    INNER JOIN @ticketTiming tt ON tt.ticket_rn = s.ticket_rn
    WHERE s.ticket_rn = @tr;

    SET @tarrival = CAST(DATEADD(minute, -@arrivalMin, @nowMsk) AS time(0));
    SET @tcomplete = CASE
        WHEN @completeMin IS NOT NULL THEN CAST(DATEADD(minute, -@completeMin, @nowMsk) AS time(0))
        ELSE NULL
    END;

    INSERT INTO Appointment (
        id_status_app, id_category, date_arrival, time_arrival, number,
        num_pause_in_a_row, priority, info, id_client, time_complete, last_id_location)
    VALUES (
        @sap, @cat, @todayMsk, @tarrival, @num,
        0, @pri, N'-', @id_client_test + @tr, @tcomplete, NULL);

    SET @aid = SCOPE_IDENTITY();
    INSERT INTO @newAppt (ticket_rn, id_appointment) VALUES (@tr, @aid);

    INSERT INTO List_item (
        id_appointment, id_specialty, id_status_item, id_cabinet, id_doctor,
        time_call, time_start_servicing, time_end_servicing, fix_cabinet, result_received)
    SELECT
        @aid,
        cr.id_specialty,
        st.id_status_item,
        cr.id_cabinet,
        cr.id_doctor,
        CASE WHEN st.call_min IS NOT NULL THEN CAST(DATEADD(minute, -st.call_min, @nowMsk) AS time(0)) END,
        CASE WHEN st.start_min IS NOT NULL THEN CAST(DATEADD(minute, -st.start_min, @nowMsk) AS time(0)) END,
        CASE WHEN st.end_min IS NOT NULL THEN CAST(DATEADD(minute, -st.end_min, @nowMsk) AS time(0)) END,
        0,
        st.result_received
    FROM @cloneRoute cr
    INNER JOIN @stepTiming st ON st.ticket_rn = cr.ticket_rn AND st.step_rn = cr.step_rn
    WHERE cr.ticket_rn = @tr;

    SET @tr += 1;
END

COMMIT;

PRINT 'Seed complete (clone from ' + CONVERT(varchar(10), @sourceDate, 120) + ').';
PRINT 'Appointment date_arrival (dashboard, MSK): ' + CONVERT(varchar(10), @todayMsk, 120);
PRINT 'Now (MSK): ' + CONVERT(varchar(30), @nowMsk, 120);
PRINT 'Times spread by ticket (minutes before now) — see @ticketTiming / @stepTiming in script.';

SELECT s.ticket_rn, a.number, a.id_status_app, s.scenario,
       tt.arrival_min AS arrival_min_ago,
       tt.complete_min AS complete_min_ago,
       COUNT(li.id_list_item) AS route_steps,
       MAX(li.id_status_item) AS max_step_status
FROM Appointment a
INNER JOIN @newAppt na ON na.id_appointment = a.id_appointment
INNER JOIN @sources s ON s.ticket_rn = na.ticket_rn
INNER JOIN @ticketTiming tt ON tt.ticket_rn = s.ticket_rn
JOIN List_item li ON li.id_appointment = a.id_appointment
GROUP BY s.ticket_rn, a.number, a.id_status_app, s.scenario, tt.arrival_min, tt.complete_min
ORDER BY s.ticket_rn;
