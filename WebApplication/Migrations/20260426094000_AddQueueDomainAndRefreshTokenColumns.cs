using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication.Migrations
{
    public partial class AddQueueDomainAndRefreshTokenColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "refresh_token",
                table: "AspNetUsers",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "refresh_token_expires_at",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "doctor",
                columns: table => new
                {
                    doctor_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    first_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    patronymic = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    specialization = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doctor", x => x.doctor_id);
                });

            migrationBuilder.CreateTable(
                name: "patient",
                columns: table => new
                {
                    patient_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    first_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    patronymic = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient", x => x.patient_id);
                });

            migrationBuilder.CreateTable(
                name: "cabinet",
                columns: table => new
                {
                    cabinet_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    cabinet_number = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cabinet", x => x.cabinet_id);
                });

            migrationBuilder.CreateTable(
                name: "service_category",
                columns: table => new
                {
                    service_category_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_category", x => x.service_category_id);
                });

            migrationBuilder.CreateTable(
                name: "queue_entry",
                columns: table => new
                {
                    queue_entry_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    patient_id = table.Column<long>(type: "bigint", nullable: false),
                    service_category_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    queued_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    called_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_queue_entry", x => x.queue_entry_id);
                    table.ForeignKey(
                        name: "FK_queue_entry_patient_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patient",
                        principalColumn: "patient_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_queue_entry_service_category_service_category_id",
                        column: x => x.service_category_id,
                        principalTable: "service_category",
                        principalColumn: "service_category_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "appointment",
                columns: table => new
                {
                    appointment_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    queue_entry_id = table.Column<long>(type: "bigint", nullable: false),
                    doctor_id = table.Column<long>(type: "bigint", nullable: false),
                    cabinet_id = table.Column<long>(type: "bigint", nullable: false),
                    start_time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    end_time = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointment", x => x.appointment_id);
                    table.ForeignKey(
                        name: "FK_appointment_cabinet_cabinet_id",
                        column: x => x.cabinet_id,
                        principalTable: "cabinet",
                        principalColumn: "cabinet_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_appointment_doctor_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctor",
                        principalColumn: "doctor_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_appointment_queue_entry_queue_entry_id",
                        column: x => x.queue_entry_id,
                        principalTable: "queue_entry",
                        principalColumn: "queue_entry_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_appointment_cabinet_id_start_time",
                table: "appointment",
                columns: new[] { "cabinet_id", "start_time" });

            migrationBuilder.CreateIndex(
                name: "IX_appointment_doctor_id_start_time",
                table: "appointment",
                columns: new[] { "doctor_id", "start_time" });

            migrationBuilder.CreateIndex(
                name: "IX_appointment_queue_entry_id",
                table: "appointment",
                column: "queue_entry_id",
                unique: false);

            migrationBuilder.CreateIndex(
                name: "IX_queue_entry_patient_id",
                table: "queue_entry",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "IX_queue_entry_service_category_id",
                table: "queue_entry",
                column: "service_category_id");

            migrationBuilder.CreateIndex(
                name: "IX_queue_entry_status_queued_at",
                table: "queue_entry",
                columns: new[] { "status", "queued_at" });

            migrationBuilder.CreateIndex(
                name: "IX_cabinet_cabinet_number",
                table: "cabinet",
                column: "cabinet_number",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "appointment");
            migrationBuilder.DropTable(name: "queue_entry");
            migrationBuilder.DropTable(name: "cabinet");
            migrationBuilder.DropTable(name: "doctor");
            migrationBuilder.DropTable(name: "patient");
            migrationBuilder.DropTable(name: "service_category");

            migrationBuilder.DropColumn(name: "refresh_token", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "refresh_token_expires_at", table: "AspNetUsers");
        }
    }
}
