USE ElectronicQueueProf;
GO

IF OBJECT_ID(N'dbo.List_item', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.List_item (
        id_list_item int NOT NULL IDENTITY(1, 1),
        id_appointment int NOT NULL,
        id_specialty int NOT NULL,
        time_start_servicing time(0) NULL,
        time_end_servicing time(0) NULL,
        id_status_item int NOT NULL,
        id_cabinet int NULL,
        time_call time(0) NULL,
        service_time time(0) NULL,
        id_doctor int NULL,
        CONSTRAINT PK_List_item PRIMARY KEY (id_list_item)
    );
END
GO

IF OBJECT_ID(N'dbo.Appointment', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Appointment (
        id_appointment int NOT NULL IDENTITY(1, 1),
        id_category int NULL,
        date_arrival date NOT NULL,
        time_arrival time(0) NOT NULL,
        number nvarchar(64) NULL,
        time_start_pause time(0) NULL,
        priority int NOT NULL CONSTRAINT DF_Appointment_priority DEFAULT (0),
        info nvarchar(1000) NOT NULL CONSTRAINT DF_Appointment_info DEFAULT (N''),
        id_client int NULL,
        time_complete time(0) NULL,
        CONSTRAINT PK_Appointment PRIMARY KEY (id_appointment)
    );
END
GO

IF OBJECT_ID(N'dbo.Log_work', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Log_work (
        id_log_work int NOT NULL IDENTITY(1, 1),
        id_cabinet int NOT NULL,
        id_doctor int NOT NULL,
        date_work date NOT NULL,
        time_begin time(0) NULL,
        time_end time(0) NULL,
        last_refresh datetime2 NULL,
        CONSTRAINT PK_Log_work PRIMARY KEY (id_log_work)
    );
END
GO

IF OBJECT_ID(N'dbo.Category', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Category (
        id_category int NOT NULL IDENTITY(1, 1),
        id_setting int NOT NULL,
        name nvarchar(500) NOT NULL,
        priority int NOT NULL CONSTRAINT DF_Category_priority DEFAULT (0),
        CONSTRAINT PK_Category PRIMARY KEY (id_category)
    );
END
GO

IF OBJECT_ID(N'dbo.Setting_queue', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Setting_queue (
        id_setting int NOT NULL IDENTITY(1, 1),
        start_id_specialty int NOT NULL,
        end_id_specialty int NOT NULL,
        CONSTRAINT PK_Setting_queue PRIMARY KEY (id_setting)
    );
END
GO

IF OBJECT_ID(N'dbo.Specialty', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Specialty (
        id_specialty int NOT NULL IDENTITY(1, 1),
        definition nvarchar(500) NOT NULL,
        time_servicing int NOT NULL CONSTRAINT DF_Specialty_time_servicing DEFAULT (20),
        CONSTRAINT PK_Specialty PRIMARY KEY (id_specialty)
    );
END
GO

IF OBJECT_ID(N'dbo.Status_item_list', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Status_item_list (
        id_status_item int NOT NULL,
        name nvarchar(200) NOT NULL,
        CONSTRAINT PK_Status_item_list PRIMARY KEY (id_status_item)
    );
END
GO

IF OBJECT_ID(N'dbo.Doctor', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Doctor (
        id_doctor int NOT NULL IDENTITY(1, 1),
        full_name nvarchar(500) NOT NULL,
        CONSTRAINT PK_Doctor PRIMARY KEY (id_doctor)
    );
END
GO

IF OBJECT_ID(N'dbo.Cabinet', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Cabinet (
        id_cabinet int NOT NULL IDENTITY(1, 1),
        cabinet_number nvarchar(64) NOT NULL,
        CONSTRAINT PK_Cabinet PRIMARY KEY (id_cabinet)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_List_item_Appointment')
    ALTER TABLE dbo.List_item ADD CONSTRAINT FK_List_item_Appointment
        FOREIGN KEY (id_appointment) REFERENCES dbo.Appointment (id_appointment);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_List_item_Specialty')
    ALTER TABLE dbo.List_item ADD CONSTRAINT FK_List_item_Specialty
        FOREIGN KEY (id_specialty) REFERENCES dbo.Specialty (id_specialty);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_List_item_Status_item_list')
    ALTER TABLE dbo.List_item ADD CONSTRAINT FK_List_item_Status_item_list
        FOREIGN KEY (id_status_item) REFERENCES dbo.Status_item_list (id_status_item);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_List_item_Cabinet')
    ALTER TABLE dbo.List_item ADD CONSTRAINT FK_List_item_Cabinet
        FOREIGN KEY (id_cabinet) REFERENCES dbo.Cabinet (id_cabinet);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_List_item_Doctor')
    ALTER TABLE dbo.List_item ADD CONSTRAINT FK_List_item_Doctor
        FOREIGN KEY (id_doctor) REFERENCES dbo.Doctor (id_doctor);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Appointment_Category')
    ALTER TABLE dbo.Appointment ADD CONSTRAINT FK_Appointment_Category
        FOREIGN KEY (id_category) REFERENCES dbo.Category (id_category);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Category_Setting_queue')
    ALTER TABLE dbo.Category ADD CONSTRAINT FK_Category_Setting_queue
        FOREIGN KEY (id_setting) REFERENCES dbo.Setting_queue (id_setting);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Log_work_Cabinet')
    ALTER TABLE dbo.Log_work ADD CONSTRAINT FK_Log_work_Cabinet
        FOREIGN KEY (id_cabinet) REFERENCES dbo.Cabinet (id_cabinet);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Log_work_Doctor')
    ALTER TABLE dbo.Log_work ADD CONSTRAINT FK_Log_work_Doctor
        FOREIGN KEY (id_doctor) REFERENCES dbo.Doctor (id_doctor);
GO
