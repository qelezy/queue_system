/*
  ElectronicQueueProf — снятие схемы (колонки, PK, FK).
  Запуск (Windows Auth, локальный SQLEXPRESS):

    sqlcmd -S .\SQLEXPRESS -E -d ElectronicQueueProf -i export-electronic-queue-schema.sql

  Результат выводится в консоль; для файла:

    sqlcmd -S .\SQLEXPRESS -E -d ElectronicQueueProf -i export-electronic-queue-schema.sql -o schema-export.txt
*/
SET NOCOUNT ON;
USE ElectronicQueueProf;
GO

PRINT '=== TABLES ===';
SELECT t.TABLE_SCHEMA, t.TABLE_NAME, t.TABLE_TYPE
FROM INFORMATION_SCHEMA.TABLES t
WHERE t.TABLE_TYPE = 'BASE TABLE'
ORDER BY t.TABLE_NAME;
GO

PRINT '=== COLUMNS ===';
SELECT
    c.TABLE_NAME,
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.CHARACTER_MAXIMUM_LENGTH,
    c.NUMERIC_PRECISION,
    c.NUMERIC_SCALE,
    c.IS_NULLABLE,
    c.COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_SCHEMA = N'dbo'
ORDER BY c.TABLE_NAME, c.ORDINAL_POSITION;
GO

PRINT '=== PRIMARY KEYS ===';
SELECT
    tc.TABLE_NAME,
    kcu.COLUMN_NAME,
    kcu.ORDINAL_POSITION
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
    ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
    AND tc.TABLE_SCHEMA = kcu.TABLE_SCHEMA
WHERE tc.CONSTRAINT_TYPE = N'PRIMARY KEY'
  AND tc.TABLE_SCHEMA = N'dbo'
ORDER BY tc.TABLE_NAME, kcu.ORDINAL_POSITION;
GO

PRINT '=== FOREIGN KEYS ===';
SELECT
    fk.name AS FK_NAME,
    OBJECT_NAME(fk.parent_object_id) AS CHILD_TABLE,
    COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS CHILD_COLUMN,
    OBJECT_NAME(fk.referenced_object_id) AS PARENT_TABLE,
    COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS PARENT_COLUMN
FROM sys.foreign_keys fk
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
ORDER BY CHILD_TABLE, FK_NAME;
GO
