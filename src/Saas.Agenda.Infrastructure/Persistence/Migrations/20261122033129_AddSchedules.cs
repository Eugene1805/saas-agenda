using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saas.Agenda.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clinic_schedules",
                schema: "agenda",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clinic_schedules", x => x.tenant_id);
                });

            migrationBuilder.CreateTable(
                name: "specialist_schedule_exceptions",
                schema: "agenda",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    specialist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date_gmt6 = table.Column<DateOnly>(type: "date", nullable: false),
                    is_unavailable = table.Column<bool>(type: "boolean", nullable: false),
                    custom_start_time = table.Column<TimeOnly>(type: "time", nullable: true),
                    custom_end_time = table.Column<TimeOnly>(type: "time", nullable: true),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    created_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_specialist_schedule_exceptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "specialist_schedules",
                schema: "agenda",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    specialist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    use_clinic_fallback = table.Column<bool>(type: "boolean", nullable: false),
                    updated_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_specialist_schedules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "clinic_schedule_slots",
                schema: "agenda",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clinic_schedule_slots", x => x.id);
                    table.ForeignKey(
                        name: "FK_clinic_schedule_slots_clinic_schedules_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "agenda",
                        principalTable: "clinic_schedules",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "specialist_schedule_slots",
                schema: "agenda",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    specialist_schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_specialist_schedule_slots", x => x.id);
                    table.ForeignKey(
                        name: "FK_specialist_schedule_slots_specialist_schedules_specialist_s~",
                        column: x => x.specialist_schedule_id,
                        principalSchema: "agenda",
                        principalTable: "specialist_schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_clinic_schedule_slots_tenant_day",
                schema: "agenda",
                table: "clinic_schedule_slots",
                columns: new[] { "tenant_id", "day_of_week", "start_time" });

            migrationBuilder.CreateIndex(
                name: "ux_specialist_schedule_exceptions_tenant_spec_date",
                schema: "agenda",
                table: "specialist_schedule_exceptions",
                columns: new[] { "tenant_id", "specialist_id", "date_gmt6" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_specialist_schedule_slots_parent_day",
                schema: "agenda",
                table: "specialist_schedule_slots",
                columns: new[] { "specialist_schedule_id", "day_of_week", "start_time" });

            migrationBuilder.CreateIndex(
                name: "ux_specialist_schedules_tenant_spec",
                schema: "agenda",
                table: "specialist_schedules",
                columns: new[] { "tenant_id", "specialist_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clinic_schedule_slots",
                schema: "agenda");

            migrationBuilder.DropTable(
                name: "specialist_schedule_exceptions",
                schema: "agenda");

            migrationBuilder.DropTable(
                name: "specialist_schedule_slots",
                schema: "agenda");

            migrationBuilder.DropTable(
                name: "clinic_schedules",
                schema: "agenda");

            migrationBuilder.DropTable(
                name: "specialist_schedules",
                schema: "agenda");
        }
    }
}
