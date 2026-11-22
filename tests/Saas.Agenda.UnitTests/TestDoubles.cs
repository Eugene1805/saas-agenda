using Saas.Agenda.Application.Ports;
using Saas.Agenda.Domain.Common;
using Saas.Agenda.Domain.Entities;
using Saas.Agenda.Domain.Policies;
using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.UnitTests;

internal static class TestFixtures
{
    public static readonly Guid NorteTenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid ValleTenantId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid AuroraTenantId = Guid.Parse("10000000-0000-0000-0000-000000000003");

    public static readonly Guid SuperAdminId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid AdminNorteId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid MendozaNorteId = Guid.Parse("20000000-0000-0000-0000-000000000003");
    public static readonly Guid SalazarNorteId = Guid.Parse("20000000-0000-0000-0000-000000000004");
    public static readonly Guid AuxNorteId = Guid.Parse("20000000-0000-0000-0000-000000000005");
    public static readonly Guid VegaInactiveNorteId = Guid.Parse("20000000-0000-0000-0000-000000000006");
    public static readonly Guid AdminValleId = Guid.Parse("20000000-0000-0000-0000-000000000007");
    public static readonly Guid MendozaValleId = Guid.Parse("20000000-0000-0000-0000-000000000008");

    public static readonly Guid Pat0101Id = Guid.Parse("30000000-0000-0000-0000-000000000101");
    public static readonly Guid Pat0102Id = Guid.Parse("30000000-0000-0000-0000-000000000102");
    public static readonly Guid Pat0103UnassignedId = Guid.Parse("30000000-0000-0000-0000-000000000103");
    public static readonly Guid Pat0104ExceptionalId = Guid.Parse("30000000-0000-0000-0000-000000000104");
    public static readonly Guid Pat0201ValleId = Guid.Parse("30000000-0000-0000-0000-000000000201");

    public static readonly AgendaActorContext SuperAdminActor = new(SuperAdminId, null, OrgRole.SUPER_ADMIN, "Operador Plataforma");
    public static readonly AgendaActorContext AdminNorteActor = new(AdminNorteId, NorteTenantId, OrgRole.ADMIN_CLINICA, "Lic. Mariana Paredes");
    public static readonly AgendaActorContext AuxNorteActor = new(AuxNorteId, NorteTenantId, OrgRole.AUXILIAR, "Enf. Clara Ramírez");
    public static readonly AgendaActorContext MendozaNorteActor = new(MendozaNorteId, NorteTenantId, OrgRole.ESPECIALISTA, "Dra. Sofía Mendoza");
    public static readonly AgendaActorContext SalazarNorteActor = new(SalazarNorteId, NorteTenantId, OrgRole.ESPECIALISTA, "Dr. Mateo Salazar");
    public static readonly AgendaActorContext AdminValleActor = new(AdminValleId, ValleTenantId, OrgRole.ADMIN_CLINICA, "Ing. Rodrigo Téllez");
}

internal sealed class FakeClock(DateTimeOffset initialUtcNow) : IClockPort
{
    public DateTimeOffset UtcNow { get; set; } = initialUtcNow.ToUniversalTime();
}

internal sealed class InMemoryAppointmentRepository : IAppointmentRepository
{
    public List<Appointment> Items { get; } = [];
    public bool SimulateDbScheduleConflict { get; set; }

    public Task<Appointment?> GetByIdAsync(
        Guid tenantId,
        Guid appointmentId,
        bool includeHistory,
        CancellationToken cancellationToken = default)
    {
        var found = Items.FirstOrDefault(a => a.TenantId == tenantId && a.Id == appointmentId);
        return Task.FromResult(found);
    }

    public Task<IReadOnlyList<Appointment>> ListAsync(
        AppointmentQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        if (filter.RestrictToPatientIds is not null && filter.RestrictToPatientIds.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<Appointment>>([]);
        }

        IEnumerable<Appointment> query = Items.Where(a => a.TenantId == filter.TenantId);

        if (filter.SpecialistId is Guid specialistId)
        {
            query = query.Where(a => a.SpecialistId == specialistId);
        }

        if (filter.PatientId is Guid patientId)
        {
            query = query.Where(a => a.PatientId == patientId);
        }

        if (filter.Status is AppointmentStatus status)
        {
            query = query.Where(a => a.Status == status);
        }

        if (filter.FromUtc is DateTimeOffset fromUtc)
        {
            query = query.Where(a => a.ScheduledStartUtc >= fromUtc);
        }

        if (filter.ToExclusiveUtc is DateTimeOffset toExclusiveUtc)
        {
            query = query.Where(a => a.ScheduledStartUtc < toExclusiveUtc);
        }

        if (filter.RestrictToPatientIds is not null)
        {
            query = query.Where(a => filter.RestrictToPatientIds.Contains(a.PatientId));
        }

        IReadOnlyList<Appointment> result = query
            .OrderBy(a => a.ScheduledStartUtc)
            .ThenBy(a => a.Id)
            .ToArray();

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Appointment>> ListActiveBySpecialistInRangeAsync(
        Guid tenantId,
        Guid specialistId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        Guid? excludeAppointmentId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Appointment> result = Items
            .Where(a =>
                a.TenantId == tenantId &&
                a.SpecialistId == specialistId &&
                a.BlocksSchedule &&
                (excludeAppointmentId is null || a.Id != excludeAppointmentId.Value) &&
                a.ScheduledStartUtc < endUtc &&
                a.ScheduledEndUtc > startUtc)
            .ToArray();

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Appointment>> ListFutureActiveBySpecialistAsync(
        Guid tenantId,
        Guid specialistId,
        DateTimeOffset fromUtc,
        CancellationToken cancellationToken = default)
    {
        var normalizedFrom = fromUtc.ToUniversalTime();
        IReadOnlyList<Appointment> result = Items
            .Where(a =>
                a.TenantId == tenantId &&
                a.SpecialistId == specialistId &&
                a.BlocksSchedule &&
                a.ScheduledStartUtc >= normalizedFrom)
            .OrderBy(a => a.ScheduledStartUtc)
            .ToArray();

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Appointment>> ListFutureActiveByTenantExcludingSpecialistsAsync(
        Guid tenantId,
        IReadOnlySet<Guid> excludedSpecialistIds,
        DateTimeOffset fromUtc,
        CancellationToken cancellationToken = default)
    {
        var normalizedFrom = fromUtc.ToUniversalTime();
        IReadOnlyList<Appointment> result = Items
            .Where(a =>
                a.TenantId == tenantId &&
                !excludedSpecialistIds.Contains(a.SpecialistId) &&
                a.BlocksSchedule &&
                a.ScheduledStartUtc >= normalizedFrom)
            .OrderBy(a => a.ScheduledStartUtc)
            .ToArray();

        return Task.FromResult(result);
    }

    public Task<IReadOnlyDictionary<AppointmentStatus, int>> CountByStatusAsync(
        Guid? tenantId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toExclusiveUtc,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<Appointment> query = Items;

        if (tenantId is Guid tid)
        {
            query = query.Where(a => a.TenantId == tid);
        }

        if (fromUtc is DateTimeOffset f)
        {
            query = query.Where(a => a.ScheduledStartUtc >= f);
        }

        if (toExclusiveUtc is DateTimeOffset t)
        {
            query = query.Where(a => a.ScheduledStartUtc < t);
        }

        IReadOnlyDictionary<AppointmentStatus, int> counts = query
            .GroupBy(a => a.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        return Task.FromResult(counts);
    }

    public Task<RepositoryWriteOutcome> AddAsync(
        Appointment appointment,
        AppointmentHistoryEntry initialHistory,
        CancellationToken cancellationToken = default)
    {
        if (SimulateDbScheduleConflict ||
            AppointmentPolicy.HasOverlap(appointment.ScheduledStartUtc, appointment.ScheduledEndUtc, Items.Where(a => a.TenantId == appointment.TenantId && a.SpecialistId == appointment.SpecialistId)))
        {
            return Task.FromResult(RepositoryWriteOutcome.ScheduleConflict);
        }

        Items.Add(appointment);
        return Task.FromResult(RepositoryWriteOutcome.Succeeded);
    }

    public Task<RepositoryWriteOutcome> UpdateWithHistoryAsync(
        Appointment appointment,
        AppointmentHistoryEntry historyEntry,
        CancellationToken cancellationToken = default)
    {
        if (SimulateDbScheduleConflict)
        {
            return Task.FromResult(RepositoryWriteOutcome.ScheduleConflict);
        }

        return Task.FromResult(RepositoryWriteOutcome.Succeeded);
    }
}

internal sealed class InMemoryScheduleRepository : IScheduleRepository
{
    public Dictionary<Guid, ClinicSchedule> ClinicSchedules { get; } = [];
    public List<SpecialistSchedule> SpecialistSchedules { get; } = [];
    public List<SpecialistScheduleException> Exceptions { get; } = [];

    public void SeedDefaultNorteSchedules(DateTimeOffset utcNow)
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

        var (clinicSchedule, _) = ClinicSchedule.Create(
            TestFixtures.NorteTenantId,
            clinicSlots,
            TestFixtures.AdminNorteId,
            "Lic. Mariana Paredes",
            utcNow);

        ClinicSchedules[TestFixtures.NorteTenantId] = clinicSchedule;

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
            Guid.NewGuid(),
            TestFixtures.NorteTenantId,
            TestFixtures.MendozaNorteId,
            useClinicFallback: false,
            mendozaSlots,
            TestFixtures.AdminNorteId,
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
            Guid.NewGuid(),
            TestFixtures.NorteTenantId,
            TestFixtures.SalazarNorteId,
            useClinicFallback: false,
            salazarSlots,
            TestFixtures.AdminNorteId,
            "Lic. Mariana Paredes",
            utcNow);

        SpecialistSchedules.Add(mendozaSchedule);
        SpecialistSchedules.Add(salazarSchedule);
    }

    public Task<ClinicSchedule?> GetClinicScheduleAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        ClinicSchedules.TryGetValue(tenantId, out var found);
        return Task.FromResult(found);
    }

    public Task SaveClinicScheduleAsync(
        ClinicSchedule schedule,
        bool isNew,
        IReadOnlyList<ClinicScheduleSlot> newSlots,
        CancellationToken cancellationToken = default)
    {
        ClinicSchedules[schedule.TenantId] = schedule;
        return Task.CompletedTask;
    }

    public Task<SpecialistSchedule?> GetSpecialistScheduleAsync(
        Guid tenantId,
        Guid specialistId,
        CancellationToken cancellationToken = default)
    {
        var found = SpecialistSchedules.FirstOrDefault(s =>
            s.TenantId == tenantId && s.SpecialistId == specialistId);
        return Task.FromResult(found);
    }

    public Task<IReadOnlySet<Guid>> ListCustomSpecialistIdsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlySet<Guid> ids = SpecialistSchedules
            .Where(s => s.TenantId == tenantId && !s.UseClinicFallback)
            .Select(s => s.SpecialistId)
            .ToHashSet();

        return Task.FromResult(ids);
    }

    public Task SaveSpecialistScheduleAsync(
        SpecialistSchedule schedule,
        bool isNew,
        IReadOnlyList<SpecialistScheduleSlot> newSlots,
        CancellationToken cancellationToken = default)
    {
        if (isNew)
        {
            SpecialistSchedules.Add(schedule);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SpecialistScheduleException>> ListSpecialistExceptionsAsync(
        Guid tenantId,
        Guid specialistId,
        DateOnly? fromDateGmt6 = null,
        CancellationToken cancellationToken = default)
    {
        var query = Exceptions.Where(e => e.TenantId == tenantId && e.SpecialistId == specialistId);
        if (fromDateGmt6 is DateOnly fromDate)
        {
            query = query.Where(e => e.DateGmt6 >= fromDate);
        }

        IReadOnlyList<SpecialistScheduleException> result = query
            .OrderBy(e => e.DateGmt6)
            .ToArray();

        return Task.FromResult(result);
    }

    public Task<SpecialistScheduleException?> GetSpecialistExceptionByDateAsync(
        Guid tenantId,
        Guid specialistId,
        DateOnly dateGmt6,
        CancellationToken cancellationToken = default)
    {
        var found = Exceptions.FirstOrDefault(e =>
            e.TenantId == tenantId && e.SpecialistId == specialistId && e.DateGmt6 == dateGmt6);
        return Task.FromResult(found);
    }

    public Task<SpecialistScheduleException?> GetSpecialistExceptionByIdAsync(
        Guid tenantId,
        Guid specialistId,
        Guid exceptionId,
        CancellationToken cancellationToken = default)
    {
        var found = Exceptions.FirstOrDefault(e =>
            e.TenantId == tenantId && e.SpecialistId == specialistId && e.Id == exceptionId);
        return Task.FromResult(found);
    }

    public Task UpsertSpecialistExceptionAsync(
        SpecialistScheduleException exception,
        bool isNew,
        CancellationToken cancellationToken = default)
    {
        if (isNew)
        {
            Exceptions.Add(exception);
        }

        return Task.CompletedTask;
    }

    public Task DeleteSpecialistExceptionAsync(
        SpecialistScheduleException exception,
        CancellationToken cancellationToken = default)
    {
        Exceptions.RemoveAll(e => e.Id == exception.Id);
        return Task.CompletedTask;
    }
}

internal sealed class StubTenantAccessStatePort : ITenantAccessStatePort
{
    public bool Unavailable { get; set; }

    public Task<TenantLookupResult> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (Unavailable)
        {
            return Task.FromResult(TenantLookupResult.Unavailable());
        }

        if (tenantId == TestFixtures.NorteTenantId)
        {
            return Task.FromResult(TenantLookupResult.Found(new TenantAccessState(
                TestFixtures.NorteTenantId,
                "clinica-norte",
                "Clínica Médica del Norte",
                TenantOperationalState.ACTIVA,
                true,
                Gmt6Clock.FormatIsoGmt6(DateTimeOffset.UtcNow))));
        }

        if (tenantId == TestFixtures.ValleTenantId)
        {
            return Task.FromResult(TenantLookupResult.Found(new TenantAccessState(
                TestFixtures.ValleTenantId,
                "clinica-valle",
                "Consultorios del Valle",
                TenantOperationalState.SUSPENDIDA,
                false,
                Gmt6Clock.FormatIsoGmt6(DateTimeOffset.UtcNow))));
        }

        if (tenantId == TestFixtures.AuroraTenantId)
        {
            return Task.FromResult(TenantLookupResult.Found(new TenantAccessState(
                TestFixtures.AuroraTenantId,
                "clinica-aurora",
                "Unidad Clínica Aurora",
                TenantOperationalState.PENDIENTE_ADMIN,
                false,
                Gmt6Clock.FormatIsoGmt6(DateTimeOffset.UtcNow))));
        }

        return Task.FromResult(TenantLookupResult.NotFound());
    }
}

internal sealed class StubAgendaDirectoryPort : IAgendaDirectoryPort
{
    public bool Unavailable { get; set; }

    private static readonly SpecialistSummary[] Specialists =
    [
        new(TestFixtures.MendozaNorteId, TestFixtures.NorteTenantId, "Dra. Sofía Mendoza", "Cardiología", true),
        new(TestFixtures.SalazarNorteId, TestFixtures.NorteTenantId, "Dr. Mateo Salazar", "Medicina General", true),
        new(TestFixtures.VegaInactiveNorteId, TestFixtures.NorteTenantId, "Dr. Nicolás Vega", "Pediatría", false),
        new(TestFixtures.MendozaValleId, TestFixtures.ValleTenantId, "Dra. Sofía Mendoza (Valle)", "Cardiología", true),
    ];

    public Task<SpecialistLookupResult> GetSpecialistAsync(
        Guid tenantId,
        Guid specialistId,
        CancellationToken cancellationToken = default)
    {
        if (Unavailable)
        {
            return Task.FromResult(SpecialistLookupResult.Unavailable());
        }

        var match = Specialists.FirstOrDefault(s => s.TenantId == tenantId && s.SpecialistId == specialistId);
        return Task.FromResult(match is null
            ? SpecialistLookupResult.NotFound()
            : SpecialistLookupResult.Found(match));
    }
}

internal sealed class StubAgendaAccessControlPort : IAgendaAccessControlPort
{
    public bool Unavailable { get; set; }

    public Task<AccessControlCheckResult> EvaluatePatientAccessAsync(
        Guid actorAccountId,
        Guid tenantId,
        OrgRole actorRole,
        Guid patientId,
        AgendaAccessOperation operation,
        CancellationToken cancellationToken = default)
    {
        if (Unavailable)
        {
            return Task.FromResult(AccessControlCheckResult.Unavailable());
        }

        var belongsToNorte = tenantId == TestFixtures.NorteTenantId &&
            (patientId == TestFixtures.Pat0101Id ||
             patientId == TestFixtures.Pat0102Id ||
             patientId == TestFixtures.Pat0103UnassignedId ||
             patientId == TestFixtures.Pat0104ExceptionalId);

        var belongsToValle = tenantId == TestFixtures.ValleTenantId &&
            patientId == TestFixtures.Pat0201ValleId;

        if (!belongsToNorte && !belongsToValle)
        {
            return Task.FromResult(AccessControlCheckResult.PatientNotFound());
        }

        if (actorRole == OrgRole.ADMIN_CLINICA)
        {
            return Task.FromResult(AccessControlCheckResult.Allowed());
        }

        if (actorRole == OrgRole.AUXILIAR && actorAccountId == TestFixtures.AuxNorteId)
        {
            var isAuthorized = patientId == TestFixtures.Pat0101Id ||
                patientId == TestFixtures.Pat0103UnassignedId ||
                patientId == TestFixtures.Pat0104ExceptionalId;

            return Task.FromResult(isAuthorized
                ? AccessControlCheckResult.Allowed()
                : AccessControlCheckResult.Denied(
                    "OUT_OF_AGENDA_SCOPE",
                    "El auxiliar no tiene ámbito ordinario ni permiso excepcional vigente para este paciente."));
        }

        return Task.FromResult(AccessControlCheckResult.Denied("FORBIDDEN_AGENDA_ROLE", "Acceso denegado."));
    }

    public Task<AuthorizedPatientsScopeResult> GetAuthorizedPatientIdsAsync(
        Guid actorAccountId,
        Guid tenantId,
        OrgRole actorRole,
        CancellationToken cancellationToken = default)
    {
        if (Unavailable)
        {
            return Task.FromResult(AuthorizedPatientsScopeResult.Unavailable());
        }

        if (actorRole == OrgRole.ADMIN_CLINICA)
        {
            return Task.FromResult(AuthorizedPatientsScopeResult.AllTenantPatients());
        }

        if (actorRole == OrgRole.AUXILIAR && tenantId == TestFixtures.NorteTenantId && actorAccountId == TestFixtures.AuxNorteId)
        {
            return Task.FromResult(AuthorizedPatientsScopeResult.RestrictedSet(new HashSet<Guid>
            {
                TestFixtures.Pat0101Id,
                TestFixtures.Pat0103UnassignedId,
                TestFixtures.Pat0104ExceptionalId,
            }));
        }

        return Task.FromResult(AuthorizedPatientsScopeResult.RestrictedSet(new HashSet<Guid>()));
    }
}
