using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WebApplication.Data;

#nullable disable

namespace WebApplication.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260618120000_RenameUserDbTablesToPascalCase")]
    public partial class RenameUserDbTablesToPascalCase : Migration
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
                      AND name COLLATE Latin1_General_BIN2 = N'roles')
                   AND NOT EXISTS (
                    SELECT 1 FROM sys.tables
                    WHERE schema_id = SCHEMA_ID(N'dbo')
                      AND name COLLATE Latin1_General_BIN2 = N'Role')
                    EXEC sp_rename N'[dbo].[roles]', N'Role', N'OBJECT';

                IF EXISTS (
                    SELECT 1 FROM sys.tables
                    WHERE schema_id = SCHEMA_ID(N'dbo')
                      AND name COLLATE Latin1_General_BIN2 = N'permission')
                BEGIN
                    EXEC sp_rename N'[dbo].[permission]', N'Permission__CaseFixTmp', N'OBJECT';
                    EXEC sp_rename N'[dbo].[Permission__CaseFixTmp]', N'Permission', N'OBJECT';
                END

                IF EXISTS (
                    SELECT 1 FROM sys.tables
                    WHERE schema_id = SCHEMA_ID(N'dbo')
                      AND name COLLATE Latin1_General_BIN2 = N'role_permission')
                   AND NOT EXISTS (
                    SELECT 1 FROM sys.tables
                    WHERE schema_id = SCHEMA_ID(N'dbo')
                      AND name COLLATE Latin1_General_BIN2 = N'RolePermission')
                    EXEC sp_rename N'[dbo].[role_permission]', N'RolePermission', N'OBJECT';
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
                      AND name COLLATE Latin1_General_BIN2 = N'Role')
                   AND NOT EXISTS (
                    SELECT 1 FROM sys.tables
                    WHERE schema_id = SCHEMA_ID(N'dbo')
                      AND name COLLATE Latin1_General_BIN2 = N'roles')
                    EXEC sp_rename N'[dbo].[Role]', N'roles', N'OBJECT';

                IF EXISTS (
                    SELECT 1 FROM sys.tables
                    WHERE schema_id = SCHEMA_ID(N'dbo')
                      AND name COLLATE Latin1_General_BIN2 = N'Permission')
                BEGIN
                    EXEC sp_rename N'[dbo].[Permission]', N'Permission__CaseFixTmp', N'OBJECT';
                    EXEC sp_rename N'[dbo].[Permission__CaseFixTmp]', N'permission', N'OBJECT';
                END

                IF EXISTS (
                    SELECT 1 FROM sys.tables
                    WHERE schema_id = SCHEMA_ID(N'dbo')
                      AND name COLLATE Latin1_General_BIN2 = N'RolePermission')
                   AND NOT EXISTS (
                    SELECT 1 FROM sys.tables
                    WHERE schema_id = SCHEMA_ID(N'dbo')
                      AND name COLLATE Latin1_General_BIN2 = N'role_permission')
                    EXEC sp_rename N'[dbo].[RolePermission]', N'role_permission', N'OBJECT';
                """);
        }
    }
}
