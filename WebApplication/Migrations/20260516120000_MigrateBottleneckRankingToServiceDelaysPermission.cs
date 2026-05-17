using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WebApplication.Data;

#nullable disable

namespace WebApplication.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260516120000_MigrateBottleneckRankingToServiceDelaysPermission")]
public partial class MigrateBottleneckRankingToServiceDelaysPermission : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[dbo].[role_permission]', N'U') IS NOT NULL
               AND OBJECT_ID(N'[dbo].[permission]', N'U') IS NOT NULL
            BEGIN
                DECLARE @legacy_id INT = (
                    SELECT TOP (1) [permission_id]
                    FROM [dbo].[permission]
                    WHERE [permission_name] = N'bottleneck-ranking');
                DECLARE @current_id INT = (
                    SELECT TOP (1) [permission_id]
                    FROM [dbo].[permission]
                    WHERE [permission_name] = N'service-delays');

                IF @legacy_id IS NOT NULL AND @current_id IS NULL
                BEGIN
                    UPDATE [dbo].[permission]
                    SET [permission_name] = N'service-delays'
                    WHERE [permission_id] = @legacy_id;
                END
                ELSE IF @legacy_id IS NOT NULL AND @current_id IS NOT NULL AND @legacy_id <> @current_id
                BEGIN
                    INSERT INTO [dbo].[role_permission] ([role_id], [permission_id])
                    SELECT rp.[role_id], @current_id
                    FROM [dbo].[role_permission] AS rp
                    WHERE rp.[permission_id] = @legacy_id
                      AND NOT EXISTS (
                          SELECT 1
                          FROM [dbo].[role_permission] AS existing
                          WHERE existing.[role_id] = rp.[role_id]
                            AND existing.[permission_id] = @current_id);

                    DELETE rp
                    FROM [dbo].[role_permission] AS rp
                    WHERE rp.[permission_id] = @legacy_id;

                    DELETE FROM [dbo].[permission]
                    WHERE [permission_id] = @legacy_id;
                END
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Не откатываем: идентификатор отчёта в приложении — service-delays.
    }
}
