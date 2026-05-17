using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WebApplication.Data;

#nullable disable

namespace WebApplication.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260517120000_MigrateUnservedToNoShowsPermission")]
public partial class MigrateUnservedToNoShowsPermission : Migration
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
                    WHERE [permission_name] = N'unserved-and-chain-breaks');
                DECLARE @current_id INT = (
                    SELECT TOP (1) [permission_id]
                    FROM [dbo].[permission]
                    WHERE [permission_name] = N'no-shows-and-incomplete-service');

                IF @legacy_id IS NOT NULL AND @current_id IS NULL
                BEGIN
                    UPDATE [dbo].[permission]
                    SET [permission_name] = N'no-shows-and-incomplete-service'
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
        // Не откатываем: идентификатор отчёта в приложении — no-shows-and-incomplete-service.
    }
}
