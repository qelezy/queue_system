using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WebApplication.Data;

#nullable disable

namespace WebApplication.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260426155000_EnsureIdentityTablesNormalized")]
    public partial class EnsureIdentityTablesNormalized : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[user]', N'U') IS NULL
                BEGIN
                    IF OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NOT NULL
                        EXEC sp_rename N'[dbo].[AspNetUsers]', N'user';
                    ELSE IF OBJECT_ID(N'[dbo].[app_user]', N'U') IS NOT NULL
                        EXEC sp_rename N'[dbo].[app_user]', N'user';
                END

                IF OBJECT_ID(N'[dbo].[roles]', N'U') IS NULL
                BEGIN
                    IF OBJECT_ID(N'[dbo].[AspNetRoles]', N'U') IS NOT NULL
                        EXEC sp_rename N'[dbo].[AspNetRoles]', N'roles';
                    ELSE IF OBJECT_ID(N'[dbo].[role]', N'U') IS NOT NULL
                        EXEC sp_rename N'[dbo].[role]', N'roles';
                    ELSE IF OBJECT_ID(N'[dbo].[app_role]', N'U') IS NOT NULL
                        EXEC sp_rename N'[dbo].[app_role]', N'roles';
                END

                IF OBJECT_ID(N'[dbo].[roles]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[roles](
                        [Id] nvarchar(450) NOT NULL,
                        [Name] nvarchar(256) NULL,
                        [NormalizedName] nvarchar(256) NULL,
                        [ConcurrencyStamp] nvarchar(max) NULL,
                        CONSTRAINT [PK_roles] PRIMARY KEY ([Id])
                    );

                    CREATE UNIQUE INDEX [RoleNameIndex]
                    ON [dbo].[roles] ([NormalizedName])
                    WHERE [NormalizedName] IS NOT NULL;
                END

                IF OBJECT_ID(N'[dbo].[user]', N'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH(N'[dbo].[user]', N'first_name') IS NULL
                        ALTER TABLE [dbo].[user] ADD [first_name] nvarchar(100) NOT NULL CONSTRAINT [DF_user_first_name_fix] DEFAULT N'';

                    IF COL_LENGTH(N'[dbo].[user]', N'last_name') IS NULL
                        ALTER TABLE [dbo].[user] ADD [last_name] nvarchar(100) NOT NULL CONSTRAINT [DF_user_last_name_fix] DEFAULT N'';

                    IF COL_LENGTH(N'[dbo].[user]', N'patronymic') IS NULL
                        ALTER TABLE [dbo].[user] ADD [patronymic] nvarchar(100) NULL;
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[roles]', N'U') IS NOT NULL
                   AND OBJECT_ID(N'[dbo].[role]', N'U') IS NULL
                    EXEC sp_rename N'[dbo].[roles]', N'role';

                IF OBJECT_ID(N'[dbo].[user]', N'U') IS NOT NULL
                   AND OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NULL
                    EXEC sp_rename N'[dbo].[user]', N'AspNetUsers';
                """);
        }
    }
}
