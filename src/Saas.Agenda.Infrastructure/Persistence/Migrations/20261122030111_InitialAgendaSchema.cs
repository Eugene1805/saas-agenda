using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saas.Agenda.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAgendaSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "agenda");

            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            migrationBuilder.CreateTable(
                name: "appointments",
                schema: "agenda",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    specialist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_start_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    scheduled_end_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "appointment_history",
                schema: "agenda",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    from_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    to_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    previous_start_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    new_start_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    previous_duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    new_duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    actor_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointment_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_appointment_history_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalSchema: "agenda",
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_history_apt_occurred",
                schema: "agenda",
                table: "appointment_history",
                columns: new[] { "appointment_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_appointments_tenant_patient",
                schema: "agenda",
                table: "appointments",
                columns: new[] { "tenant_id", "patient_id" });

            migrationBuilder.CreateIndex(
                name: "ix_appointments_tenant_specialist_start",
                schema: "agenda",
                table: "appointments",
                columns: new[] { "tenant_id", "specialist_id", "scheduled_start_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_appointments_tenant_start",
                schema: "agenda",
                table: "appointments",
                columns: new[] { "tenant_id", "scheduled_start_utc" });

            migrationBuilder.Sql(
                """
                ALTER TABLE agenda.appointments
                ADD CONSTRAINT ck_appointments_valid_duration
                CHECK (duration_minutes >= 10 AND duration_minutes <= 480 AND scheduled_end_utc > scheduled_start_utc);

                ALTER TABLE agenda.appointments
                ADD CONSTRAINT ex_appointments_no_overlap
                EXCLUDE USING gist (
                    tenant_id WITH =,
                    specialist_id WITH =,
                    tstzrange(scheduled_start_utc, scheduled_end_utc, '[)') WITH &&
                )
                WHERE (status NOT IN ('CANCELADA', 'NO_ASISTIO'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE agenda.appointments DROP CONSTRAINT IF EXISTS ex_appointments_no_overlap;
                ALTER TABLE agenda.appointments DROP CONSTRAINT IF EXISTS ck_appointments_valid_duration;
                """);

            migrationBuilder.DropTable(
                name: "appointment_history",
                schema: "agenda");

            migrationBuilder.DropTable(
                name: "appointments",
                schema: "agenda");
        }
    }
}
