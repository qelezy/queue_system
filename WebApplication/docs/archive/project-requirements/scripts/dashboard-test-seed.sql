SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @nowMsk datetime2 = SYSDATETIME();
DECLARE @todayMsk date = CAST(@nowMsk AS date);
DECLARE @ticketCount int = 20;
DECLARE @clientIdMin int = 99990001;
DECLARE @clientIdMax int = 99990020;

IF EXISTS (
    SELECT 1 FROM Appointment
    WHERE id_client BETWEEN @clientIdMin AND @clientIdMax
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
DECLARE @sa_wait int = 1, @sa_called int = 2, @sa_service int = 3, @sa_done int = 4, @sa_pause int = 5;

DECLARE @ticketTiming TABLE (
    ticket_rn int NOT NULL PRIMARY KEY,
    arrival_min int NOT NULL,
    complete_min int NULL
);

INSERT INTO @ticketTiming (ticket_rn, arrival_min, complete_min) VALUES
    (1, 18, NULL),
    (2, 12, NULL),
    (3, 20, NULL),
    (4, 17, NULL),
    (5, 22, NULL),
    (6, 19, NULL),
    (7, 24, 3),
    (8, 16, NULL),
    (9, 14, NULL),
    (10, 13, NULL),
    (11, 15, NULL),
    (12, 18, NULL),
    (13, 12, NULL),
    (14, 19, NULL),
    (15, 16, NULL),
    (16, 21, NULL),
    (17, 14, NULL),
    (18, 20, 3),
    (19, 17, 5),
    (20, 18, NULL);

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
    (3, 1, 19, 17, 14, @st_done, 1),
    (3, 2, 13, 12, 6, @st_done, 1),
    (3, 3, 5, NULL, NULL, @st_called, 0),
    (4, 1, 16, 14, 11, @st_done, 1),
    (4, 2, 10, 8, 5, @st_done, 1),
    (4, 3, 4, NULL, NULL, @st_called, 0),
    (5, 1, 21, 19, 15, @st_done, 1),
    (5, 2, 14, 12, 8, @st_done, 1),
    (5, 3, 7, 4, NULL, @st_service, 0),
    (6, 1, 18, 16, 13, @st_done, 1),
    (6, 2, 12, 10, 7, @st_done, 1),
    (6, 3, 6, 3, NULL, @st_service, 0),
    (7, 1, 22, 20, 16, @st_done, 1),
    (7, 2, 15, 13, 9, @st_done, 1),
    (7, 3, 8, 6, 3, @st_done, 1),
    (8, 1, 14, 12, 8, @st_done, 1),
    (8, 2, 7, 5, 4, @st_done, 1),
    (8, 3, NULL, NULL, NULL, @st_results, 0),
    (9, 1, NULL, NULL, NULL, @st_wait, 0),
    (10, 1, 12, 10, 7, @st_done, 1),
    (10, 2, NULL, NULL, NULL, @st_wait, 0),
    (11, 1, NULL, NULL, NULL, @st_wait, 0),
    (11, 2, NULL, NULL, NULL, @st_wait, 0),
    (11, 3, NULL, NULL, NULL, @st_wait, 0),
    (12, 1, NULL, NULL, NULL, @st_wait, 0),
    (12, 2, NULL, NULL, NULL, @st_wait, 0),
    (12, 3, NULL, NULL, NULL, @st_wait, 0),
    (13, 1, NULL, NULL, NULL, @st_wait, 0),
    (13, 2, NULL, NULL, NULL, @st_wait, 0),
    (14, 1, 18, 16, 13, @st_done, 1),
    (14, 2, 11, 9, 6, @st_done, 1),
    (14, 3, 5, 2, NULL, @st_service, 0),
    (15, 1, 15, 13, 10, @st_done, 1),
    (15, 2, 9, 7, 4, @st_done, 1),
    (15, 3, 3, 1, NULL, @st_service, 0),
    (16, 1, 20, 18, 15, @st_done, 1),
    (16, 2, 13, 11, 8, @st_done, 1),
    (16, 3, 6, 3, NULL, @st_service, 0),
    (17, 1, 13, 11, 8, @st_done, 1),
    (17, 2, 7, 5, 3, @st_done, 1),
    (17, 3, 2, 1, NULL, @st_service, 0),
    (18, 1, 18, 16, 12, @st_done, 1),
    (18, 2, 12, 10, 7, @st_done, 1),
    (18, 3, 6, 4, 2, @st_done, 1),
    (19, 1, 15, 13, 10, @st_done, 1),
    (19, 2, 10, 8, 5, @st_done, 1),
    (19, 3, 4, 2, 2, @st_done, 1),
    (20, 1, 16, 14, 10, @st_done, 1),
    (20, 2, 10, 8, 4, @st_done, 1),
    (20, 3, NULL, NULL, NULL, @st_results, 0);

DECLARE @sourcePlan TABLE (
    ticket_rn int NOT NULL PRIMARY KEY,
    id_status_app int NOT NULL,
    scenario nvarchar(20) NOT NULL,
    required_steps int NOT NULL
);

INSERT INTO @sourcePlan (ticket_rn, id_status_app, scenario, required_steps)
SELECT tt.ticket_rn,
       CASE
           WHEN v.scenario = N'called' THEN @sa_called
           WHEN v.scenario = N'service' THEN @sa_service
           WHEN v.scenario = N'done' THEN @sa_done
           WHEN v.scenario = N'edge_pause' THEN @sa_pause
           ELSE @sa_wait
       END,
       v.scenario,
       MAX(st.step_rn)
FROM @ticketTiming tt
INNER JOIN (VALUES
    (1, N'wait'),
    (2, N'wait'),
    (3, N'called'),
    (4, N'called'),
    (5, N'service'),
    (6, N'service'),
    (7, N'done'),
    (8, N'results'),
    (9, N'edge_single'),
    (10, N'edge_pause'),
    (11, N'wait'),
    (12, N'wait'),
    (13, N'wait'),
    (14, N'service'),
    (15, N'service'),
    (16, N'service'),
    (17, N'service'),
    (18, N'done'),
    (19, N'done'),
    (20, N'results')
) v(ticket_rn, scenario) ON v.ticket_rn = tt.ticket_rn
INNER JOIN @stepTiming st ON st.ticket_rn = tt.ticket_rn
GROUP BY tt.ticket_rn, v.scenario;

DECLARE @routeCandidates TABLE (
    id_appointment int NOT NULL PRIMARY KEY,
    date_arrival date NOT NULL,
    letter varchar(1) NOT NULL,
    source_number varchar(32) NOT NULL,
    route_steps int NOT NULL
);

INSERT INTO @routeCandidates (id_appointment, date_arrival, letter, source_number, route_steps)
SELECT a.id_appointment, a.date_arrival, cat.letter, a.number, COUNT(*) AS route_steps
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
GROUP BY a.id_appointment, a.date_arrival, cat.letter, a.number
HAVING COUNT(*) BETWEEN 1 AND 3
   AND SUM(CASE
       WHEN li.id_doctor IS NULL
         OR li.id_doctor <= 0
         OR li.id_cabinet IS NULL
         OR li.id_cabinet <= 0
         OR li.id_specialty = 25
         OR NULLIF(LTRIM(RTRIM(d.full_name)), '') IS NULL
         OR NULLIF(LTRIM(RTRIM(sp.definition)), '') IS NULL
         OR NULLIF(LTRIM(RTRIM(cab.cabinet_number)), '') IS NULL
         OR r.id_specialty IS NULL
       THEN 1 ELSE 0 END) = 0;

DECLARE @sources TABLE (
    ticket_rn int NOT NULL PRIMARY KEY,
    source_id int NOT NULL,
    source_date date NOT NULL,
    id_status_app int NOT NULL,
    scenario nvarchar(20) NOT NULL
);

DECLARE @pickTicket int = 1;

WHILE @pickTicket <= @ticketCount
BEGIN
    INSERT INTO @sources (ticket_rn, source_id, source_date, id_status_app, scenario)
    SELECT TOP 1
        p.ticket_rn,
        rc.id_appointment,
        rc.date_arrival,
        p.id_status_app,
        p.scenario
    FROM @sourcePlan p
    INNER JOIN @routeCandidates rc ON rc.route_steps >= p.required_steps
    WHERE p.ticket_rn = @pickTicket
      AND NOT EXISTS (SELECT 1 FROM @sources s WHERE s.source_id = rc.id_appointment)
      AND NOT EXISTS (
          SELECT 1
          FROM @sources s
          INNER JOIN @routeCandidates used ON used.id_appointment = s.source_id
          WHERE used.letter = rc.letter
            AND TRY_CONVERT(int, SUBSTRING(used.source_number, LEN(used.letter) + 1, 31))
                = TRY_CONVERT(int, SUBSTRING(rc.source_number, LEN(rc.letter) + 1, 31))
      )
    ORDER BY
        CASE WHEN rc.route_steps = p.required_steps THEN 0 ELSE 1 END,
        rc.date_arrival DESC,
        rc.id_appointment DESC;

    SET @pickTicket += 1;
END;

IF (SELECT COUNT(*) FROM @sources) <> @ticketCount
BEGIN
    RAISERROR('Could not select 20 valid source routes from current database.', 16, 1);
    RETURN;
END;

DECLARE @cloneRoute TABLE (
    ticket_rn int NOT NULL,
    step_rn int NOT NULL,
    id_category int NOT NULL,
    letter varchar(1) NOT NULL,
    source_number varchar(32) NOT NULL,
    priority int NOT NULL,
    id_specialty int NOT NULL,
    id_cabinet int NULL,
    id_doctor int NULL,
    PRIMARY KEY (ticket_rn, step_rn)
);

INSERT INTO @cloneRoute (ticket_rn, step_rn, id_category, letter, source_number, priority, id_specialty, id_cabinet, id_doctor)
SELECT
    s.ticket_rn,
    x.step_rn,
    a.id_category,
    cat.letter,
    a.number,
    a.priority,
    x.id_specialty,
    x.id_cabinet,
    x.id_doctor
FROM @sources s
INNER JOIN Appointment a ON a.id_appointment = s.source_id AND a.date_arrival = s.source_date
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
WHERE x.step_rn <= 3
  AND x.step_rn <= (
      SELECT MAX(st.step_rn)
      FROM @stepTiming st
      WHERE st.ticket_rn = s.ticket_rn
  );

IF (SELECT COUNT(DISTINCT ticket_rn) FROM @cloneRoute) < @ticketCount
BEGIN
    RAISERROR('Could not load routes for all 20 source tickets.', 16, 1);
    RETURN;
END;

DECLARE @doctorRefs TABLE (
    ref_rn int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
    id_doctor int NOT NULL,
    id_specialty int NOT NULL,
    id_cabinet int NOT NULL
);

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
INSERT INTO @doctorRefs (id_doctor, id_specialty, id_cabinet)
SELECT TOP 12 id_doctor, id_specialty, id_cabinet
FROM RankedPairs
WHERE rn = 1
ORDER BY cnt DESC, id_doctor;

IF (SELECT COUNT(*) FROM @doctorRefs) < 12
BEGIN
    RAISERROR('Need at least 12 doctors with valid specialty/cabinet pairs.', 16, 1);
    RETURN;
END;

DECLARE @currentDoctorPlan TABLE (
    ticket_rn int NOT NULL PRIMARY KEY,
    step_rn int NOT NULL,
    ref_rn int NOT NULL
);

INSERT INTO @currentDoctorPlan (ticket_rn, step_rn, ref_rn) VALUES
    (1, 1, 1),
    (2, 1, 1),
    (3, 3, 7),
    (4, 3, 8),
    (5, 3, 2),
    (6, 3, 3),
    (8, 3, 9),
    (9, 1, 10),
    (10, 2, 11),
    (11, 1, 12),
    (12, 1, 9),
    (13, 1, 10),
    (14, 3, 1),
    (15, 3, 4),
    (16, 3, 5),
    (17, 3, 6),
    (20, 3, 11);

UPDATE cr
SET cr.id_doctor = dr.id_doctor,
    cr.id_specialty = dr.id_specialty,
    cr.id_cabinet = dr.id_cabinet
FROM @cloneRoute cr
INNER JOIN @currentDoctorPlan cdp ON cdp.ticket_rn = cr.ticket_rn AND cdp.step_rn = cr.step_rn
INNER JOIN @doctorRefs dr ON dr.ref_rn = cdp.ref_rn;

UPDATE cr
SET cr.priority = v.priority
FROM @cloneRoute cr
INNER JOIN (VALUES
    (1, 2), (2, 0), (3, 1), (4, 0), (8, 1), (9, 2), (10, 0),
    (11, 3), (12, 1), (13, 0), (20, 1)
) v(ticket_rn, priority) ON v.ticket_rn = cr.ticket_rn
WHERE cr.step_rn = 1;

IF EXISTS (
    SELECT 1
    FROM @cloneRoute cr
    LEFT JOIN Doctor d ON d.id_doctor = cr.id_doctor
    LEFT JOIN Specialty sp ON sp.id_specialty = cr.id_specialty
    LEFT JOIN Cabinet cab ON cab.id_cabinet = cr.id_cabinet
    LEFT JOIN Refer r ON r.id_specialty = cr.id_specialty AND r.id_cabinet = cr.id_cabinet
    WHERE cr.id_doctor IS NULL
       OR cr.id_doctor <= 0
       OR cr.id_cabinet IS NULL
       OR cr.id_cabinet <= 0
       OR cr.id_specialty = 25
       OR NULLIF(LTRIM(RTRIM(d.full_name)), '') IS NULL
       OR NULLIF(LTRIM(RTRIM(sp.definition)), '') IS NULL
       OR NULLIF(LTRIM(RTRIM(cab.cabinet_number)), '') IS NULL
       OR r.id_specialty IS NULL
)
BEGIN
    RAISERROR('Selected routes contain invalid doctor, specialty, cabinet, or refer data.', 16, 1);
    RETURN;
END;

DECLARE @id_client_test int = 99990000;

DECLARE @nums TABLE (
    ticket_rn int NOT NULL PRIMARY KEY,
    ticket_num int NOT NULL,
    letter varchar(1) NOT NULL
);

INSERT INTO @nums (ticket_rn, ticket_num, letter)
SELECT
    cr.ticket_rn,
    COALESCE(
        TRY_CONVERT(int, SUBSTRING(cr.source_number, LEN(cr.letter) + 1, 31)),
        cr.ticket_rn
    ),
    cr.letter
FROM @cloneRoute cr
WHERE cr.step_rn = 1;

DECLARE @newAppt TABLE (ticket_rn int NOT NULL PRIMARY KEY, id_appointment int NOT NULL);

BEGIN TRAN;

SET IDENTITY_INSERT Numbers ON;
INSERT INTO Numbers (Number)
SELECT ticket_num FROM @nums;
SET IDENTITY_INSERT Numbers OFF;

INSERT INTO Log_work (id_cabinet, id_doctor, date_work, time_begin, time_end, last_refresh)
SELECT DISTINCT cr.id_cabinet, cr.id_doctor, @todayMsk, CAST('08:00' AS time), NULL, GETDATE()
FROM @cloneRoute cr
INNER JOIN @sources s ON s.ticket_rn = cr.ticket_rn
WHERE cr.id_doctor IS NOT NULL
  AND cr.id_cabinet IS NOT NULL
  AND s.scenario IN (N'wait', N'called', N'service', N'results', N'edge_single', N'edge_pause')
  AND NOT EXISTS (
      SELECT 1 FROM Log_work lw
      WHERE lw.id_doctor = cr.id_doctor
        AND lw.date_work = @todayMsk
        AND lw.time_begin IS NOT NULL
        AND lw.time_end IS NULL
  );

DECLARE @tr int = 1;
DECLARE @aid int, @num nvarchar(32), @cat int, @pri int, @sap int;
DECLARE @arrivalMin int, @completeMin int;
DECLARE @tarrival time, @tcomplete time;

WHILE @tr <= @ticketCount
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

PRINT 'Seed complete: 20 tickets cloned from multiple source dates.';
PRINT 'Appointment date_arrival (dashboard, MSK): ' + CONVERT(varchar(10), @todayMsk, 120);
PRINT 'Now (MSK): ' + CONVERT(varchar(30), @nowMsk, 120);

SELECT s.ticket_rn, s.source_id, s.source_date, a.number, a.id_status_app, s.scenario,
       tt.arrival_min AS arrival_min_ago,
       tt.complete_min AS complete_min_ago,
       COUNT(li.id_list_item) AS route_steps,
       MAX(li.id_status_item) AS max_step_status
FROM Appointment a
INNER JOIN @newAppt na ON na.id_appointment = a.id_appointment
INNER JOIN @sources s ON s.ticket_rn = na.ticket_rn
INNER JOIN @ticketTiming tt ON tt.ticket_rn = s.ticket_rn
JOIN List_item li ON li.id_appointment = a.id_appointment
GROUP BY s.ticket_rn, s.source_id, s.source_date, a.number, a.id_status_app, s.scenario, tt.arrival_min, tt.complete_min
ORDER BY s.ticket_rn;
