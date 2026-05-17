using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WebApplication.Data;

#nullable disable

namespace WebApplication.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260517130000_RemoveMultiAndSingleStageRoutesPermission")]
public partial class RemoveMultiAndSingleStageRoutesPermission : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[dbo].[role_permission]', N'U') IS NOT NULL
               AND OBJECT_ID(N'[dbo].[permission]', N'U') IS NOT NULL
            BEGIN
                DELETE rp
                FROM [dbo].[role_permission] AS rp
                INNER JOIN [dbo].[permission] AS p ON rp.[permission_id] = p.[permission_id]
                WHERE p.[permission_name] = N'multi-and-single-stage-routes';

                DELETE FROM [dbo].[permission]
                WHERE [permission_name] = N'multi-and-single-stage-routes';
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Не восстанавливаем: id отчёта удалён из каталога.
    }
}
