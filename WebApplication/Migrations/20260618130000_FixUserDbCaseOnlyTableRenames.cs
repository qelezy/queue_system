using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WebApplication.Data;

#nullable disable

namespace WebApplication.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260618130000_FixUserDbCaseOnlyTableRenames")]
    public partial class FixUserDbCaseOnlyTableRenames : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1 FROM sys.tables
                    WHERE schema_id = SCHEMA_ID(N'dbo')
                      AND name COLLATE Latin1_General_BIN2 = N'user')
                BEGIN
                    EXEC sp_rename N'[dbo].[user]', N'User__CaseFixTmp', N'OBJECT';
                    EXEC sp_rename N'[dbo].[User__CaseFixTmp]', N'User', N'OBJECT';
                END

                IF EXISTS (
                    SELECT 1 FROM sys.tables
                    WHERE schema_id = SCHEMA_ID(N'dbo')
                      AND name COLLATE Latin1_General_BIN2 = N'permission')
                BEGIN
                    EXEC sp_rename N'[dbo].[permission]', N'Permission__CaseFixTmp', N'OBJECT';
                    EXEC sp_rename N'[dbo].[Permission__CaseFixTmp]', N'Permission', N'OBJECT';
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1 FROM sys.tables
                    WHERE schema_id = SCHEMA_ID(N'dbo')
                      AND name COLLATE Latin1_General_BIN2 = N'User')
                BEGIN
                    EXEC sp_rename N'[dbo].[User]', N'User__CaseFixTmp', N'OBJECT';
                    EXEC sp_rename N'[dbo].[User__CaseFixTmp]', N'user', N'OBJECT';
                END

                IF EXISTS (
                    SELECT 1 FROM sys.tables
                    WHERE schema_id = SCHEMA_ID(N'dbo')
                      AND name COLLATE Latin1_General_BIN2 = N'Permission')
                BEGIN
                    EXEC sp_rename N'[dbo].[Permission]', N'Permission__CaseFixTmp', N'OBJECT';
                    EXEC sp_rename N'[dbo].[Permission__CaseFixTmp]', N'permission', N'OBJECT';
                END
                """);
        }
    }
}
