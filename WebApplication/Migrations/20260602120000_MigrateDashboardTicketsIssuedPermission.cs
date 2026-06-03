using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WebApplication.Data;

#nullable disable

namespace WebApplication.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260602120000_MigrateDashboardTicketsIssuedPermission")]
public partial class MigrateDashboardTicketsIssuedPermission : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[dbo].[role_permission]', N'U') IS NOT NULL
               AND OBJECT_ID(N'[dbo].[permission]', N'U') IS NOT NULL
            BEGIN
                DECLARE @tickets_id INT;
                DECLARE @avg_wait_id INT;
                DECLARE @avg_service_id INT;

                IF NOT EXISTS (
                    SELECT 1 FROM [dbo].[permission]
                    WHERE [permission_name] = N'dashboard.tickets-issued')
                BEGIN
                    INSERT INTO [dbo].[permission] ([permission_name])
                    VALUES (N'dashboard.tickets-issued');
                END

                SET @tickets_id = (
                    SELECT TOP (1) [permission_id]
                    FROM [dbo].[permission]
                    WHERE [permission_name] = N'dashboard.tickets-issued');

                SET @avg_wait_id = (
                    SELECT TOP (1) [permission_id]
                    FROM [dbo].[permission]
                    WHERE [permission_name] = N'dashboard.avg-wait');

                SET @avg_service_id = (
                    SELECT TOP (1) [permission_id]
                    FROM [dbo].[permission]
                    WHERE [permission_name] = N'dashboard.avg-service');

                IF @tickets_id IS NOT NULL
                BEGIN
                    INSERT INTO [dbo].[role_permission] ([role_id], [permission_id])
                    SELECT DISTINCT rp.[role_id], @tickets_id
                    FROM [dbo].[role_permission] AS rp
                    WHERE (
                            @avg_wait_id IS NOT NULL AND rp.[permission_id] = @avg_wait_id
                         OR @avg_service_id IS NOT NULL AND rp.[permission_id] = @avg_service_id
                          )
                      AND NOT EXISTS (
                          SELECT 1 FROM [dbo].[role_permission] AS existing
                          WHERE existing.[role_id] = rp.[role_id]
                            AND existing.[permission_id] = @tickets_id);
                END

                IF @avg_wait_id IS NOT NULL
                BEGIN
                    DELETE FROM [dbo].[role_permission] WHERE [permission_id] = @avg_wait_id;
                    DELETE FROM [dbo].[permission] WHERE [permission_id] = @avg_wait_id;
                END

                IF @avg_service_id IS NOT NULL
                BEGIN
                    DELETE FROM [dbo].[role_permission] WHERE [permission_id] = @avg_service_id;
                    DELETE FROM [dbo].[permission] WHERE [permission_id] = @avg_service_id;
                END
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[dbo].[role_permission]', N'U') IS NOT NULL
               AND OBJECT_ID(N'[dbo].[permission]', N'U') IS NOT NULL
            BEGIN
                DECLARE @tickets_id INT;
                DECLARE @avg_wait_id INT;
                DECLARE @avg_service_id INT;

                IF NOT EXISTS (
                    SELECT 1 FROM [dbo].[permission]
                    WHERE [permission_name] = N'dashboard.avg-wait')
                BEGIN
                    INSERT INTO [dbo].[permission] ([permission_name])
                    VALUES (N'dashboard.avg-wait');
                END

                IF NOT EXISTS (
                    SELECT 1 FROM [dbo].[permission]
                    WHERE [permission_name] = N'dashboard.avg-service')
                BEGIN
                    INSERT INTO [dbo].[permission] ([permission_name])
                    VALUES (N'dashboard.avg-service');
                END

                SET @avg_wait_id = (
                    SELECT TOP (1) [permission_id]
                    FROM [dbo].[permission]
                    WHERE [permission_name] = N'dashboard.avg-wait');

                SET @avg_service_id = (
                    SELECT TOP (1) [permission_id]
                    FROM [dbo].[permission]
                    WHERE [permission_name] = N'dashboard.avg-service');

                SET @tickets_id = (
                    SELECT TOP (1) [permission_id]
                    FROM [dbo].[permission]
                    WHERE [permission_name] = N'dashboard.tickets-issued');

                IF @tickets_id IS NOT NULL AND (@avg_wait_id IS NOT NULL OR @avg_service_id IS NOT NULL)
                BEGIN
                    IF @avg_wait_id IS NOT NULL
                    BEGIN
                        INSERT INTO [dbo].[role_permission] ([role_id], [permission_id])
                        SELECT DISTINCT rp.[role_id], @avg_wait_id
                        FROM [dbo].[role_permission] AS rp
                        WHERE rp.[permission_id] = @tickets_id
                          AND NOT EXISTS (
                              SELECT 1 FROM [dbo].[role_permission] AS existing
                              WHERE existing.[role_id] = rp.[role_id]
                                AND existing.[permission_id] = @avg_wait_id);
                    END

                    IF @avg_service_id IS NOT NULL
                    BEGIN
                        INSERT INTO [dbo].[role_permission] ([role_id], [permission_id])
                        SELECT DISTINCT rp.[role_id], @avg_service_id
                        FROM [dbo].[role_permission] AS rp
                        WHERE rp.[permission_id] = @tickets_id
                          AND NOT EXISTS (
                              SELECT 1 FROM [dbo].[role_permission] AS existing
                              WHERE existing.[role_id] = rp.[role_id]
                                AND existing.[permission_id] = @avg_service_id);
                    END

                    DELETE FROM [dbo].[role_permission] WHERE [permission_id] = @tickets_id;
                    DELETE FROM [dbo].[permission] WHERE [permission_id] = @tickets_id;
                END
            END
            """);
    }
}
