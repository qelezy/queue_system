USE ElectronicQueueProf;
GO

SET NOCOUNT ON;

IF EXISTS (SELECT 1 FROM dbo.Appointment WHERE id_client BETWEEN 99990101 AND 99990104)
BEGIN
    PRINT 'Docker demo queue data already present — skip seed.';
END
ELSE
BEGIN
    DECLARE @today date = CAST(SYSDATETIME() AS date);
    DECLARE @tMinus30 time(0) = CAST(DATEADD(minute, -30, CAST(SYSDATETIME() AS datetime2)) AS time(0));
    DECLARE @tMinus15 time(0) = CAST(DATEADD(minute, -15, CAST(SYSDATETIME() AS datetime2)) AS time(0));
    DECLARE @tMinus5 time(0) = CAST(DATEADD(minute, -5, CAST(SYSDATETIME() AS datetime2)) AS time(0));

    SET IDENTITY_INSERT dbo.Cabinet ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Cabinet WHERE id_cabinet = 1)
        INSERT INTO dbo.Cabinet (id_cabinet, cabinet_number) VALUES (1, N'101');
    IF NOT EXISTS (SELECT 1 FROM dbo.Cabinet WHERE id_cabinet = 2)
        INSERT INTO dbo.Cabinet (id_cabinet, cabinet_number) VALUES (2, N'102');
    SET IDENTITY_INSERT dbo.Cabinet OFF;

    SET IDENTITY_INSERT dbo.Doctor ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Doctor WHERE id_doctor = 1)
        INSERT INTO dbo.Doctor (id_doctor, full_name) VALUES (1, N'Иванов И.И.');
    IF NOT EXISTS (SELECT 1 FROM dbo.Doctor WHERE id_doctor = 2)
        INSERT INTO dbo.Doctor (id_doctor, full_name) VALUES (2, N'Петров П.П.');
    SET IDENTITY_INSERT dbo.Doctor OFF;

    IF NOT EXISTS (SELECT 1 FROM dbo.Status_item_list WHERE id_status_item = 1)
        INSERT INTO dbo.Status_item_list (id_status_item, name) VALUES (1, N'Ожидает');
    IF NOT EXISTS (SELECT 1 FROM dbo.Status_item_list WHERE id_status_item = 2)
        INSERT INTO dbo.Status_item_list (id_status_item, name) VALUES (2, N'Вызван');
    IF NOT EXISTS (SELECT 1 FROM dbo.Status_item_list WHERE id_status_item = 3)
        INSERT INTO dbo.Status_item_list (id_status_item, name) VALUES (3, N'На приёме');
    IF NOT EXISTS (SELECT 1 FROM dbo.Status_item_list WHERE id_status_item = 4)
        INSERT INTO dbo.Status_item_list (id_status_item, name) VALUES (4, N'Завершён');

    SET IDENTITY_INSERT dbo.Specialty ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Specialty WHERE id_specialty = 1)
        INSERT INTO dbo.Specialty (id_specialty, definition, time_servicing) VALUES (1, N'Терапия', 20);
    IF NOT EXISTS (SELECT 1 FROM dbo.Specialty WHERE id_specialty = 2)
        INSERT INTO dbo.Specialty (id_specialty, definition, time_servicing) VALUES (2, N'Хирургия', 25);
    SET IDENTITY_INSERT dbo.Specialty OFF;

    SET IDENTITY_INSERT dbo.Setting_queue ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Setting_queue WHERE id_setting = 1)
        INSERT INTO dbo.Setting_queue (id_setting, start_id_specialty, end_id_specialty, time_pause, critical_num_pause, name) VALUES (1, 1, 2, 5, 3, N'ОМС');
    SET IDENTITY_INSERT dbo.Setting_queue OFF;

    SET IDENTITY_INSERT dbo.Category ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Category WHERE id_category = 1)
        INSERT INTO dbo.Category (id_category, id_setting, name, priority, letter, old) VALUES (1, 1, N'ОМС', 1, N'A', 0);
    SET IDENTITY_INSERT dbo.Category OFF;

    SET IDENTITY_INSERT dbo.Appointment ON;
    INSERT INTO dbo.Appointment (id_appointment, id_category, date_arrival, time_arrival, number, priority, info, id_client, time_complete)
    VALUES
        (1, 1, @today, @tMinus30, N'DEMO-001', 1, N'-', 99990101, NULL),
        (2, 1, @today, @tMinus30, N'DEMO-002', 1, N'-', 99990102, NULL),
        (3, 1, @today, @tMinus30, N'DEMO-003', 1, N'-', 99990103, NULL),
        (4, 1, @today, @tMinus30, N'DEMO-004', 1, N'-', 99990104, @tMinus5);
    SET IDENTITY_INSERT dbo.Appointment OFF;

    SET IDENTITY_INSERT dbo.List_item ON;
    INSERT INTO dbo.List_item (id_list_item, id_appointment, id_specialty, time_start_servicing, time_end_servicing, id_status_item, id_cabinet, time_call, id_doctor)
    VALUES
        (1, 1, 1, NULL, NULL, 1, 1, NULL, 1),
        (2, 2, 1, NULL, NULL, 2, 1, @tMinus15, 1),
        (3, 3, 2, @tMinus5, NULL, 3, 2, @tMinus15, 2),
        (4, 4, 2, @tMinus30, @tMinus5, 4, 2, @tMinus30, 2);
    SET IDENTITY_INSERT dbo.List_item OFF;

    INSERT INTO dbo.Log_work (id_cabinet, id_doctor, date_work, time_begin, time_end, last_refresh)
    SELECT 1, 1, @today, CAST(N'08:00' AS time(0)), NULL, SYSDATETIME()
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Log_work WHERE id_cabinet = 1 AND id_doctor = 1 AND date_work = @today);

    INSERT INTO dbo.Log_work (id_cabinet, id_doctor, date_work, time_begin, time_end, last_refresh)
    SELECT 2, 2, @today, CAST(N'08:00' AS time(0)), NULL, SYSDATETIME()
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Log_work WHERE id_cabinet = 2 AND id_doctor = 2 AND date_work = @today);

    PRINT 'Docker demo queue data seeded.';
END
GO
