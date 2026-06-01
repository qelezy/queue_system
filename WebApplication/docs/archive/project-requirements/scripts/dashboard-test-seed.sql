-- Test data: clone 8 tickets from 2026-05-06 to today (Configurator + /dashboard).
-- Marker: info = N'-', id_client 99990001..99990008. Rollback: dashboard-test-rollback.sql
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @sourceDate date = '2026-05-06';
DECLARE @nowMsk datetime2 = SYSDATETIME();
DECLARE @todayMsk date = CAST(@nowMsk AS date);
DECLARE @tNow time(0) = CAST(@nowMsk AS time(0));

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
-- id_status_app 5 в этой БД = «На паузе», не использовать для талона «Ожидание результатов»

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

-- Ticket 5: эталон 239070 step 3 — id_specialty 25 (процедурный кабинет), Doctor-плейсхолдер; для теста — 3-й этап 239065 (без id 25)
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
DECLARE @aid int, @num nvarchar(32), @cat int, @pri int, @sap int, @scen nvarchar(20);
DECLARE @info nvarchar(200);
DECLARE @tcomplete time;

WHILE @tr <= 8
BEGIN
    SELECT
        @cat = cr.id_category,
        @pri = cr.priority,
        @sap = s.id_status_app,
        @scen = s.scenario,
        @num = n.letter + CAST(n.ticket_num AS nvarchar(31))
    FROM @sources s
    INNER JOIN @nums n ON n.ticket_rn = s.ticket_rn
    INNER JOIN @cloneRoute cr ON cr.ticket_rn = s.ticket_rn AND cr.step_rn = 1
    WHERE s.ticket_rn = @tr;

    SET @info = N'-';
    SET @tcomplete = CASE WHEN @scen = N'done' THEN @tNow ELSE NULL END;

    INSERT INTO Appointment (
        id_status_app, id_category, date_arrival, time_arrival, number,
        num_pause_in_a_row, priority, info, id_client, time_complete, last_id_location)
    VALUES (
        @sap, @cat, @todayMsk, @tNow, @num,
        0, @pri, @info, @id_client_test + @tr, @tcomplete, NULL);

    SET @aid = SCOPE_IDENTITY();
    INSERT INTO @newAppt (ticket_rn, id_appointment) VALUES (@tr, @aid);

    IF @scen = N'wait'
    BEGIN
        INSERT INTO List_item (
            id_appointment, id_specialty, id_status_item, id_cabinet, id_doctor,
            time_call, time_start_servicing, time_end_servicing, fix_cabinet, result_received)
        SELECT @aid, cr.id_specialty, @st_wait, cr.id_cabinet, cr.id_doctor,
            NULL, NULL, NULL, 0, 0
        FROM @cloneRoute cr WHERE cr.ticket_rn = @tr;
    END
    ELSE IF @scen = N'called'
    BEGIN
        INSERT INTO List_item (
            id_appointment, id_specialty, id_status_item, id_cabinet, id_doctor,
            time_call, time_start_servicing, time_end_servicing, fix_cabinet, result_received)
        SELECT @aid, cr.id_specialty, @st_done, cr.id_cabinet, cr.id_doctor,
            @tNow, @tNow, @tNow, 0, 1
        FROM @cloneRoute cr WHERE cr.ticket_rn = @tr AND cr.step_rn < 3;

        INSERT INTO List_item (
            id_appointment, id_specialty, id_status_item, id_cabinet, id_doctor,
            time_call, time_start_servicing, time_end_servicing, fix_cabinet, result_received)
        SELECT @aid, cr.id_specialty, @st_called, cr.id_cabinet, cr.id_doctor,
            @tNow, NULL, NULL, 0, 0
        FROM @cloneRoute cr WHERE cr.ticket_rn = @tr AND cr.step_rn = 3;
    END
    ELSE IF @scen = N'service'
    BEGIN
        INSERT INTO List_item (
            id_appointment, id_specialty, id_status_item, id_cabinet, id_doctor,
            time_call, time_start_servicing, time_end_servicing, fix_cabinet, result_received)
        SELECT @aid, cr.id_specialty, @st_done, cr.id_cabinet, cr.id_doctor,
            @tNow, @tNow, @tNow, 0, 1
        FROM @cloneRoute cr WHERE cr.ticket_rn = @tr AND cr.step_rn < 3;

        INSERT INTO List_item (
            id_appointment, id_specialty, id_status_item, id_cabinet, id_doctor,
            time_call, time_start_servicing, time_end_servicing, fix_cabinet, result_received)
        SELECT @aid, cr.id_specialty, @st_service, cr.id_cabinet, cr.id_doctor,
            @tNow, @tNow, NULL, 0, 0
        FROM @cloneRoute cr WHERE cr.ticket_rn = @tr AND cr.step_rn = 3;
    END
    ELSE IF @scen = N'done'
    BEGIN
        INSERT INTO List_item (
            id_appointment, id_specialty, id_status_item, id_cabinet, id_doctor,
            time_call, time_start_servicing, time_end_servicing, fix_cabinet, result_received)
        SELECT @aid, cr.id_specialty, @st_done, cr.id_cabinet, cr.id_doctor,
            @tNow, @tNow, @tNow, 0, 1
        FROM @cloneRoute cr WHERE cr.ticket_rn = @tr;
    END
    ELSE IF @scen = N'results'
    BEGIN
        INSERT INTO List_item (
            id_appointment, id_specialty, id_status_item, id_cabinet, id_doctor,
            time_call, time_start_servicing, time_end_servicing, fix_cabinet, result_received)
        SELECT @aid, cr.id_specialty, @st_done, cr.id_cabinet, cr.id_doctor,
            @tNow, @tNow, @tNow, 0, 1
        FROM @cloneRoute cr WHERE cr.ticket_rn = @tr AND cr.step_rn < 3;

        INSERT INTO List_item (
            id_appointment, id_specialty, id_status_item, id_cabinet, id_doctor,
            time_call, time_start_servicing, time_end_servicing, fix_cabinet, result_received)
        SELECT @aid, cr.id_specialty, @st_results, cr.id_cabinet, cr.id_doctor,
            NULL, NULL, NULL, 0, 0
        FROM @cloneRoute cr WHERE cr.ticket_rn = @tr AND cr.step_rn = 3;
    END

    SET @tr += 1;
END

COMMIT;

PRINT 'Seed complete (clone from ' + CONVERT(varchar(10), @sourceDate, 120) + ').';
PRINT 'Appointment date_arrival (dashboard, MSK): ' + CONVERT(varchar(10), @todayMsk, 120);
PRINT 'Now (MSK): ' + CONVERT(varchar(30), @nowMsk, 120);
PRINT 'Live times (time_arrival / open steps): ' + CONVERT(varchar(12), @tNow, 108) + ' — 0 min wait/service on dashboard';

SELECT s.ticket_rn, a.number, a.id_status_app, s.scenario,
       COUNT(li.id_list_item) AS route_steps,
       MAX(li.id_status_item) AS max_step_status
FROM Appointment a
INNER JOIN @newAppt na ON na.id_appointment = a.id_appointment
INNER JOIN @sources s ON s.ticket_rn = na.ticket_rn
JOIN List_item li ON li.id_appointment = a.id_appointment
GROUP BY s.ticket_rn, a.number, a.id_status_app, s.scenario
ORDER BY s.ticket_rn;
