using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WebApplication.Data;

#nullable disable

namespace WebApplication.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260513120000_RenameRegistratorRoleToDispatcher")]
    public partial class RenameRegistratorRoleToDispatcher : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[roles]', N'U') IS NOT NULL
                BEGIN
                    UPDATE [dbo].[roles]
                    SET [Name] = N'Dispatcher', [NormalizedName] = N'DISPATCHER'
                    WHERE [Name] = N'Registrator' OR [NormalizedName] = N'REGISTRATOR';
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[roles]', N'U') IS NOT NULL
                BEGIN
                    UPDATE [dbo].[roles]
                    SET [Name] = N'Registrator', [NormalizedName] = N'REGISTRATOR'
                    WHERE [Name] = N'Dispatcher' OR [NormalizedName] = N'DISPATCHER';
                END
                """);
        }
    }
}
