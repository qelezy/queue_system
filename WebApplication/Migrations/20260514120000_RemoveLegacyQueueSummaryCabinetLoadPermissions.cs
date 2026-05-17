using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WebApplication.Data;

#nullable disable

namespace WebApplication.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260514120000_RemoveLegacyQueueSummaryCabinetLoadPermissions")]
public partial class RemoveLegacyQueueSummaryCabinetLoadPermissions : Migration
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
                WHERE p.[permission_name] IN (N'queue-summary', N'cabinet-load');

                DELETE FROM [dbo].[permission]
                WHERE [permission_name] IN (N'queue-summary', N'cabinet-load');
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Не восстанавливаем удалённые разрешения: идентификаторы отчётов больше не используются в приложении.
    }
}
