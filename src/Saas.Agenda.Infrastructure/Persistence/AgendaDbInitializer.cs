using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Saas.Agenda.Application.Ports;
using Saas.Agenda.Domain.Common;
using Saas.Agenda.Domain.Entities;
using Saas.Agenda.Domain.ValueObjects;
using Saas.Agenda.Infrastructure.Persistence.Tenancy;

namespace Saas.Agenda.Infrastructure.Persistence;

public sealed class AgendaDbInitializer(
    ITenantAgendaDbContextFactory tenantDbContextFactory,
    ITenantDatabaseMigratorPort tenantDatabaseMigrator,
    ITenantConnectionResolver tenantConnectionResolver,
    IConfiguration configuration)
{
    public static readonly Guid NorteTenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid ValleTenantId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid AuroraTenantId = Guid.Parse("10000000-0000-0000-0000-000000000003");

    public static readonly Guid SuperAdminAccountId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid AdminNorteAccountId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid MendozaNorteAccountId = Guid.Parse("20000000-0000-0000-0000-000000000003");
    public static readonly Guid SalazarNorteAccountId = Guid.Parse("20000000-0000-0000-0000-000000000004");
    public static readonly Guid AuxRamirezNorteAccountId = Guid.Parse("20000000-0000-0000-0000-000000000005");
    public static readonly Guid VegaNorteAccountId = Guid.Parse("20000000-0000-0000-0000-000000000006");
    public static readonly Guid AdminValleAccountId = Guid.Parse("20000000-0000-0000-0000-000000000007");
    public static readonly Guid MendozaValleAccountId = Guid.Parse("20000000-0000-0000-0000-000000000008");

    public static readonly Guid Pat0101Id = Guid.Parse("30000000-0000-0000-0000-000000000101");
    public static readonly Guid Pat0102Id = Guid.Parse("30000000-0000-0000-0000-000000000102");
    public static readonly Guid Pat0103Id = Guid.Parse("30000000-0000-0000-0000-000000000103");
    public static readonly Guid Pat0104Id = Guid.Parse("30000000-0000-0000-0000-000000000104");
    public static readonly Guid Pat0201Id = Guid.Parse("30000000-0000-0000-0000-000000000201");

    public async Task InitializeConfiguredTenantsAsync(
        bool isProduction,
        CancellationToken cancellationToken = default)
    {
        foreach (var tenantId in tenantConnectionResolver.ListConfiguredTenantIds())
        {
            await tenantDatabaseMigrator.EnsureMigratedAsync(tenantId, cancellationToken);
        }

        if (isProduction || !configuration.GetValue("SeedDemoData", false))
        {
            return;
        }

        if (!tenantConnectionResolver.TryResolveConnectionString(NorteTenantId, out _))
        {
            return;
        }

        await SeedNorteTenantAsync(cancellationToken);
    }

    private async Task SeedNorteTenantAsync(CancellationToken cancellationToken)
    {
        await tenantDatabaseMigrator.EnsureMigratedAsync(NorteTenantId, cancellationToken);
        await using var dbContext = tenantDbContextFactory.CreateDbContext(NorteTenantId);

        var utcNow = DateTimeOffset.UtcNow;

        if (!await dbContext.ClinicSchedules.AnyAsync(cancellationToken))
        {
            var clinicSlots = new (ScheduleDayOfWeek Day, TimeOnly Start, TimeOnly End)[]
            {
                (ScheduleDayOfWeek.MONDAY, new TimeOnly(8, 0), new TimeOnly(20, 0)),
                (ScheduleDayOfWeek.TUESDAY, new TimeOnly(8, 0), new TimeOnly(20, 0)),
                (ScheduleDayOfWeek.WEDNESDAY, new TimeOnly(8, 0), new TimeOnly(20, 0)),
                (ScheduleDayOfWeek.THURSDAY, new TimeOnly(8, 0), new TimeOnly(20, 0)),
                (ScheduleDayOfWeek.FRIDAY, new TimeOnly(8, 0), new TimeOnly(20, 0)),
                (ScheduleDayOfWeek.SATURDAY, new TimeOnly(9, 0), new TimeOnly(14, 0)),
            };

            var (norteClinicSchedule, _) = ClinicSchedule.Create(
                NorteTenantId,
                clinicSlots,
                AdminNorteAccountId,
                "Lic. Mariana Paredes",
                utcNow);

            await dbContext.ClinicSchedules.AddAsync(norteClinicSchedule, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
        }

        if (!await dbContext.SpecialistSchedules.AnyAsync(cancellationToken))
        {
            var mendozaSlots = new List<(ScheduleDayOfWeek Day, TimeOnly Start, TimeOnly End)>();
            foreach (var day in new[]
            {
                ScheduleDayOfWeek.MONDAY,
                ScheduleDayOfWeek.TUESDAY,
                ScheduleDayOfWeek.WEDNESDAY,
                ScheduleDayOfWeek.THURSDAY,
                ScheduleDayOfWeek.FRIDAY,
            })
            {
                mendozaSlots.Add((day, new TimeOnly(9, 0), new TimeOnly(15, 0)));
                mendozaSlots.Add((day, new TimeOnly(16, 0), new TimeOnly(19, 0)));
            }

            var (mendozaSchedule, _) = SpecialistSchedule.Create(
                Guid.Parse("60000000-0000-0000-0000-000000000001"),
                NorteTenantId,
                MendozaNorteAccountId,
                useClinicFallback: false,
                mendozaSlots,
                AdminNorteAccountId,
                "Lic. Mariana Paredes",
                utcNow);

            var salazarSlots = new[]
            {
                ScheduleDayOfWeek.MONDAY,
                ScheduleDayOfWeek.TUESDAY,
                ScheduleDayOfWeek.WEDNESDAY,
                ScheduleDayOfWeek.THURSDAY,
                ScheduleDayOfWeek.FRIDAY,
                ScheduleDayOfWeek.SATURDAY,
            }
            .Select(d => (d, new TimeOnly(8, 0), new TimeOnly(16, 0)))
            .ToArray();

            var (salazarSchedule, _) = SpecialistSchedule.Create(
                Guid.Parse("60000000-0000-0000-0000-000000000002"),
                NorteTenantId,
                SalazarNorteAccountId,
                useClinicFallback: false,
                salazarSlots,
                AdminNorteAccountId,
                "Lic. Mariana Paredes",
                utcNow);

            await dbContext.SpecialistSchedules.AddRangeAsync([mendozaSchedule, salazarSchedule], cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
        }

        if (!await dbContext.Appointments.AnyAsync(cancellationToken))
        {
            var todayGmt6 = Gmt6Clock.TodayGmt6(utcNow);
            var daysUntilMonday = ((int)DayOfWeek.Monday - (int)todayGmt6.DayOfWeek + 7) % 7;
            if (daysUntilMonday == 0)
            {
                daysUntilMonday = 7;
            }

            var nextMonday = todayGmt6.AddDays(daysUntilMonday);

            var (apt1, _) = Appointment.Create(
                Guid.Parse("40000000-0000-0000-0000-000000000001"),
                NorteTenantId,
                Pat0101Id,
                MendozaNorteAccountId,
                Gmt6Clock.CombineGmt6ToUtc(nextMonday, new TimeOnly(10, 0)),
                30,
                "Control cardiológico mensual",
                AuxRamirezNorteAccountId,
                "Enf. Clara Ramírez",
                utcNow,
                Guid.Parse("50000000-0000-0000-0000-000000000001"),
                "Cita inicial sembrada para demostración en clínica-norte.");

            var (apt2, _) = Appointment.Create(
                Guid.Parse("40000000-0000-0000-0000-000000000002"),
                NorteTenantId,
                Pat0103Id,
                SalazarNorteAccountId,
                Gmt6Clock.CombineGmt6ToUtc(nextMonday, new TimeOnly(11, 30)),
                30,
                "Primera valoración administrativa (expediente pendiente de asignación)",
                AuxRamirezNorteAccountId,
                "Enf. Clara Ramírez",
                utcNow,
                Guid.Parse("50000000-0000-0000-0000-000000000002"),
                "Primera cita de paciente pendiente de asignación principal (CU-18 A7).");

            apt2.TransitionStatus(
                AppointmentStatus.CONFIRMADA,
                AuxRamirezNorteAccountId,
                "Enf. Clara Ramírez",
                "Confirmación telefónica registrada en recepción.",
                utcNow.AddMinutes(1),
                Guid.Parse("50000000-0000-0000-0000-000000000003"));

            var (apt3, _) = Appointment.Create(
                Guid.Parse("40000000-0000-0000-0000-000000000003"),
                NorteTenantId,
                Pat0102Id,
                SalazarNorteAccountId,
                Gmt6Clock.CombineGmt6ToUtc(nextMonday, new TimeOnly(14, 0)),
                30,
                "Control metabólico trimestral",
                AdminNorteAccountId,
                "Lic. Mariana Paredes",
                utcNow,
                Guid.Parse("50000000-0000-0000-0000-000000000004"),
                "Cita programada por AdminClinica fuera del ámbito ordinario de auxiliar.");

            await dbContext.Appointments.AddRangeAsync([apt1, apt2, apt3], cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
        }
    }
}
