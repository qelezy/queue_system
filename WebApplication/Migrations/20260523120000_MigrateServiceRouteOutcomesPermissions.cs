using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WebApplication.Data;

#nullable disable

namespace WebApplication.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260523120000_MigrateServiceRouteOutcomesPermissions")]
public partial class MigrateServiceRouteOutcomesPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[dbo].[role_permission]', N'U') IS NOT NULL
               AND OBJECT_ID(N'[dbo].[permission]', N'U') IS NOT NULL
            BEGIN
                DECLARE @target_id INT;
                DECLARE @source_id INT;

                -- no-shows-and-incomplete-service → service-route-outcomes
                SET @target_id = (
                    SELECT TOP (1) [permission_id]
                    FROM [dbo].[permission]
                    WHERE [permission_name] = N'service-route-outcomes');
                SET @source_id = (
                    SELECT TOP (1) [permission_id]
                    FROM [dbo].[permission]
                    WHERE [permission_name] = N'no-shows-and-incomplete-service');

                IF @source_id IS NOT NULL AND @target_id IS NULL
                BEGIN
                    UPDATE [dbo].[permission]
                    SET [permission_name] = N'service-route-outcomes'
                    WHERE [permission_id] = @source_id;
                    SET @target_id = @source_id;
                END
                ELSE IF @source_id IS NOT NULL AND @target_id IS NOT NULL AND @source_id <> @target_id
                BEGIN
                    INSERT INTO [dbo].[role_permission] ([role_id], [permission_id])
                    SELECT rp.[role_id], @target_id
                    FROM [dbo].[role_permission] AS rp
                    WHERE rp.[permission_id] = @source_id
                      AND NOT EXISTS (
                          SELECT 1 FROM [dbo].[role_permission] AS existing
                          WHERE existing.[role_id] = rp.[role_id]
                            AND existing.[permission_id] = @target_id);
                    DELETE FROM [dbo].[role_permission] WHERE [permission_id] = @source_id;
                    DELETE FROM [dbo].[permission] WHERE [permission_id] = @source_id;
                END

                -- unserved-and-chain-breaks → service-route-outcomes
                SET @source_id = (
                    SELECT TOP (1) [permission_id]
                    FROM [dbo].[permission]
                    WHERE [permission_name] = N'unserved-and-chain-breaks');
                SET @target_id = (
                    SELECT TOP (1) [permission_id]
                    FROM [dbo].[permission]
                    WHERE [permission_name] = N'service-route-outcomes');

                IF @source_id IS NOT NULL AND @target_id IS NULL
                BEGIN
                    UPDATE [dbo].[permission]
                    SET [permission_name] = N'service-route-outcomes'
                    WHERE [permission_id] = @source_id;
                END
                ELSE IF @source_id IS NOT NULL AND @target_id IS NOT NULL AND @source_id <> @target_id
                BEGIN
                    INSERT INTO [dbo].[role_permission] ([role_id], [permission_id])
                    SELECT rp.[role_id], @target_id
                    FROM [dbo].[role_permission] AS rp
                    WHERE rp.[permission_id] = @source_id
                      AND NOT EXISTS (
                          SELECT 1 FROM [dbo].[role_permission] AS existing
                          WHERE existing.[role_id] = rp.[role_id]
                            AND existing.[permission_id] = @target_id);
                    DELETE FROM [dbo].[role_permission] WHERE [permission_id] = @source_id;
                    DELETE FROM [dbo].[permission] WHERE [permission_id] = @source_id;
                END

                -- arrived-and-completed → service-route-outcomes
                SET @source_id = (
                    SELECT TOP (1) [permission_id]
                    FROM [dbo].[permission]
                    WHERE [permission_name] = N'arrived-and-completed');
                SET @target_id = (
                    SELECT TOP (1) [permission_id]
                    FROM [dbo].[permission]
                    WHERE [permission_name] = N'service-route-outcomes');

                IF @source_id IS NOT NULL AND @target_id IS NOT NULL AND @source_id <> @target_id
                BEGIN
                    INSERT INTO [dbo].[role_permission] ([role_id], [permission_id])
                    SELECT rp.[role_id], @target_id
                    FROM [dbo].[role_permission] AS rp
                    WHERE rp.[permission_id] = @source_id
                      AND NOT EXISTS (
                          SELECT 1 FROM [dbo].[role_permission] AS existing
                          WHERE existing.[role_id] = rp.[role_id]
                            AND existing.[permission_id] = @target_id);
                    DELETE FROM [dbo].[role_permission] WHERE [permission_id] = @source_id;
                    DELETE FROM [dbo].[permission] WHERE [permission_id] = @source_id;
                END
                ELSE IF @source_id IS NOT NULL AND @target_id IS NULL
                BEGIN
                    UPDATE [dbo].[permission]
                    SET [permission_name] = N'service-route-outcomes'
                    WHERE [permission_id] = @source_id;
                END

                -- dashboard.noshow-today — удалить
                SET @source_id = (
                    SELECT TOP (1) [permission_id]
                    FROM [dbo].[permission]
                    WHERE [permission_name] = N'dashboard.noshow-today');
                IF @source_id IS NOT NULL
                BEGIN
                    DELETE FROM [dbo].[role_permission] WHERE [permission_id] = @source_id;
                    DELETE FROM [dbo].[permission] WHERE [permission_id] = @source_id;
                END
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Не откатываем: идентификатор отчёта в приложении — service-route-outcomes.
    }
}
