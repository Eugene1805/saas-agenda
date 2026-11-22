using Saas.Agenda.Application.UseCases.Appointments;
using Saas.Agenda.Domain.Entities;
using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.UnitTests;

public sealed class AppointmentUseCasesTests
{
    private readonly InMemoryAppointmentRepository _repo = new();
    private readonly InMemoryScheduleRepository _schedules = new();
    private readonly StubTenantAccessStatePort _platform = new();
    private readonly StubAgendaDirectoryPort _directory = new();
    private readonly StubAgendaAccessControlPort _access = new();
    private readonly FakeClock _clock = new(DateTimeOffset.Parse("2026-11-19T10:00:00-06:00"));

    public AppointmentUseCasesTests()
    {
        _schedules.SeedDefaultNorteSchedules(_clock.UtcNow);
    }

    private CreateAppointmentUseCase CreateSut() =>
        new(_repo, _schedules, _platform, _directory, _access, _clock);

    private ListAppointmentsUseCase ListSut() =>
        new(_repo, _platform, _access);

    private GetAppointmentByIdUseCase GetSut() =>
        new(_repo, _platform, _access);

    private RescheduleAppointmentUseCase RescheduleSut() =>
        new(_repo, _schedules, _platform, _directory, _access, _clock);

    private TransitionAppointmentStatusUseCase TransitionSut() =>
        new(_repo, _platform, _access, _clock);

    private Appointment SeedAppointment(
        Guid? id = null,
        Guid? tenantId = null,
        Guid? patientId = null,
        Guid? specialistId = null,
        string startGmt6 = "2026-11-20T10:00:00-06:00",
        int duration = 30)
    {
        var (apt, _) = Appointment.Create(
            id ?? Guid.NewGuid(),
            tenantId ?? TestFixtures.NorteTenantId,
            patientId ?? TestFixtures.Pat0101Id,
            specialistId ?? TestFixtures.MendozaNorteId,
            DateTimeOffset.Parse(startGmt6).ToUniversalTime(),
            duration,
            "Control de seguimiento",
            TestFixtures.AdminNorteId,
            "Lic. Mariana Paredes",
            _clock.UtcNow);
        _repo.Items.Add(apt);
        return apt;
    }

    [Fact]
    public async Task CreateAppointment_SucceedsForAuxiliaryWithOrdinaryScopeUnassignedAndExceptionalGrant()
    {
        var sut = CreateSut();

        var ordinary = await sut.ExecuteAsync(new CreateAppointmentCommand(
            TestFixtures.AuxNorteActor,
            TestFixtures.Pat0101Id,
            TestFixtures.MendozaNorteId,
            "2026-11-20T10:00:00-06:00",
            30,
            "Control ordinario"));
        Assert.True(ordinary.IsSuccess);
        Assert.Equal("PROGRAMADA", ordinary.Value.Status);
        Assert.Equal("2026-11-20T10:00:00-06:00", ordinary.Value.ScheduledStartGmt6);

        var unassigned = await sut.ExecuteAsync(new CreateAppointmentCommand(
            TestFixtures.AuxNorteActor,
            TestFixtures.Pat0103UnassignedId,
            TestFixtures.SalazarNorteId,
            "2026-11-20T11:30:00-06:00",
            30,
            "Primera valoración sin responsable principal (CU-18 A7)"));
        Assert.True(unassigned.IsSuccess);

        var exceptional = await sut.ExecuteAsync(new CreateAppointmentCommand(
            TestFixtures.AuxNorteActor,
            TestFixtures.Pat0104ExceptionalId,
            TestFixtures.SalazarNorteId,
            "2026-11-20T12:00:00-06:00",
            30,
            "Cita por permiso excepcional MANAGE_APPOINTMENTS"));
        Assert.True(exceptional.IsSuccess);
    }

    [Fact]
    public async Task CreateAndReschedule_RejectPastTimesAndOutsideWorkingHours()
    {
        var sut = CreateSut();

        var pastCreate = await sut.ExecuteAsync(new CreateAppointmentCommand(
            TestFixtures.AdminNorteActor,
            TestFixtures.Pat0101Id,
            TestFixtures.MendozaNorteId,
            "2026-11-19T09:00:00-06:00",
            30,
            "Horario ya pasado"));
        Assert.True(pastCreate.IsFailure);
        Assert.Equal("PAST_SCHEDULE_NOT_ALLOWED", pastCreate.Error.Code);
        Assert.Equal(400, pastCreate.Error.HttpStatus);

        var lunchBreak = await sut.ExecuteAsync(new CreateAppointmentCommand(
            TestFixtures.AdminNorteActor,
            TestFixtures.Pat0101Id,
            TestFixtures.MendozaNorteId,
            "2026-11-20T15:15:00-06:00",
            30,
            "Durante comida de Dra. Mendoza"));
        Assert.True(lunchBreak.IsFailure);
        Assert.Equal("OUTSIDE_WORKING_HOURS", lunchBreak.Error.Code);
        Assert.Equal(409, lunchBreak.Error.HttpStatus);

        var sunday = await sut.ExecuteAsync(new CreateAppointmentCommand(
            TestFixtures.AdminNorteActor,
            TestFixtures.Pat0101Id,
            TestFixtures.MendozaNorteId,
            "2026-11-22T10:00:00-06:00",
            30,
            "Domingo sin horario"));
        Assert.True(sunday.IsFailure);
        Assert.Equal("OUTSIDE_WORKING_HOURS", sunday.Error.Code);

        var apt = SeedAppointment(startGmt6: "2026-11-20T10:00:00-06:00");
        var pastReschedule = await RescheduleSut().ExecuteAsync(new RescheduleAppointmentCommand(
            TestFixtures.AdminNorteActor,
            apt.Id,
            "2026-11-18T10:00:00-06:00",
            30));
        Assert.Equal("PAST_SCHEDULE_NOT_ALLOWED", pastReschedule.Error.Code);
        Assert.Equal(400, pastReschedule.Error.HttpStatus);
    }

    [Fact]
    public async Task CreateAppointment_Returns409WhenNeitherClinicNorSpecialistConfigured()
    {
        _schedules.ClinicSchedules.Clear();
        _schedules.SpecialistSchedules.Clear();

        var result = await CreateSut().ExecuteAsync(new CreateAppointmentCommand(
            TestFixtures.AdminNorteActor,
            TestFixtures.Pat0101Id,
            TestFixtures.MendozaNorteId,
            "2026-11-20T10:00:00-06:00",
            30,
            "Sin configuración horaria"));

        Assert.True(result.IsFailure);
        Assert.Equal("SCHEDULE_NOT_CONFIGURED", result.Error.Code);
        Assert.Equal(409, result.Error.HttpStatus);
    }

    [Fact]
    public async Task CreateAppointment_DeniesAuxiliaryOutOfScope()
    {
        var result = await CreateSut().ExecuteAsync(new CreateAppointmentCommand(
            TestFixtures.AuxNorteActor,
            TestFixtures.Pat0102Id,
            TestFixtures.SalazarNorteId,
            "2026-11-20T12:00:00-06:00",
            30,
            "Fuera de ámbito"));

        Assert.True(result.IsFailure);
        Assert.Equal("OUT_OF_AGENDA_SCOPE", result.Error.Code);
        Assert.Equal(403, result.Error.HttpStatus);
    }

    [Fact]
    public async Task CreateAppointment_RejectsSpecialistAndSuperAdminWriters()
    {
        var specResult = await CreateSut().ExecuteAsync(new CreateAppointmentCommand(
            TestFixtures.MendozaNorteActor,
            TestFixtures.Pat0101Id,
            TestFixtures.MendozaNorteId,
            "2026-11-20T12:00:00-06:00",
            30,
            "Intento de especialista"));
        Assert.Equal(403, specResult.Error.HttpStatus);
        Assert.Equal("FORBIDDEN_AGENDA_ROLE", specResult.Error.Code);

        var superResult = await CreateSut().ExecuteAsync(new CreateAppointmentCommand(
            TestFixtures.SuperAdminActor,
            TestFixtures.Pat0101Id,
            TestFixtures.MendozaNorteId,
            "2026-11-20T12:00:00-06:00",
            30,
            "Intento SuperAdmin"));
        Assert.Equal(403, superResult.Error.HttpStatus);
        Assert.Equal("FORBIDDEN_AGENDA_ROLE", superResult.Error.Code);
    }

    [Fact]
    public async Task CreateAppointment_RejectsInactiveOrCrossTenantSpecialist()
    {
        var inactive = await CreateSut().ExecuteAsync(new CreateAppointmentCommand(
            TestFixtures.AdminNorteActor,
            TestFixtures.Pat0101Id,
            TestFixtures.VegaInactiveNorteId,
            "2026-11-20T12:00:00-06:00",
            30,
            "Especialista inactivo"));
        Assert.True(inactive.IsFailure);
        Assert.Equal("INVALID_SPECIALIST", inactive.Error.Code);
        Assert.Equal(400, inactive.Error.HttpStatus);

        var crossTenant = await CreateSut().ExecuteAsync(new CreateAppointmentCommand(
            TestFixtures.AdminNorteActor,
            TestFixtures.Pat0101Id,
            TestFixtures.MendozaValleId,
            "2026-11-20T12:00:00-06:00",
            30,
            "Especialista de otra clínica"));
        Assert.True(crossTenant.IsFailure);
        Assert.Equal("INVALID_SPECIALIST", crossTenant.Error.Code);
    }

    [Fact]
    public async Task CreateAppointment_DetectsOverlapAndMapsConcurrentDbConflictTo409()
    {
        SeedAppointment(startGmt6: "2026-11-20T10:00:00-06:00", duration: 30);

        var overlap = await CreateSut().ExecuteAsync(new CreateAppointmentCommand(
            TestFixtures.AdminNorteActor,
            TestFixtures.Pat0102Id,
            TestFixtures.MendozaNorteId,
            "2026-11-20T10:15:00-06:00",
            30,
            "Empalme"));
        Assert.True(overlap.IsFailure);
        Assert.Equal("SCHEDULE_CONFLICT", overlap.Error.Code);
        Assert.Equal(409, overlap.Error.HttpStatus);

        _repo.SimulateDbScheduleConflict = true;
        var dbRace = await CreateSut().ExecuteAsync(new CreateAppointmentCommand(
            TestFixtures.AdminNorteActor,
            TestFixtures.Pat0102Id,
            TestFixtures.MendozaNorteId,
            "2026-11-20T16:30:00-06:00",
            30,
            "Carrera concurrente"));
        Assert.True(dbRace.IsFailure);
        Assert.Equal("SCHEDULE_CONFLICT", dbRace.Error.Code);
        Assert.Equal(409, dbRace.Error.HttpStatus);
    }

    [Fact]
    public async Task CreateAppointment_FailsClosedWhenSuspendedOrDependenciesUnavailable()
    {
        var suspended = await CreateSut().ExecuteAsync(new CreateAppointmentCommand(
            TestFixtures.AdminValleActor,
            TestFixtures.Pat0201ValleId,
            TestFixtures.MendozaValleId,
            "2026-11-20T10:00:00-06:00",
            30,
            "Clínica suspendida"));
        Assert.Equal("TENANT_SUSPENDED", suspended.Error.Code);
        Assert.Equal(403, suspended.Error.HttpStatus);

        _platform.Unavailable = true;
        var platformDown = await CreateSut().ExecuteAsync(new CreateAppointmentCommand(
            TestFixtures.AdminNorteActor,
            TestFixtures.Pat0101Id,
            TestFixtures.MendozaNorteId,
            "2026-11-20T10:00:00-06:00",
            30,
            "Platform down"));
        Assert.Equal(503, platformDown.Error.HttpStatus);
        Assert.Equal("TENANT_STATE_UNAVAILABLE", platformDown.Error.Code);
        _platform.Unavailable = false;

        _access.Unavailable = true;
        var accessDown = await CreateSut().ExecuteAsync(new CreateAppointmentCommand(
            TestFixtures.AdminNorteActor,
            TestFixtures.Pat0101Id,
            TestFixtures.MendozaNorteId,
            "2026-11-20T10:00:00-06:00",
            30,
            "Access down"));
        Assert.Equal(503, accessDown.Error.HttpStatus);
        Assert.Equal("ACCESS_CONTROL_UNAVAILABLE", accessDown.Error.Code);
        _access.Unavailable = false;

        _directory.Unavailable = true;
        var dirDown = await CreateSut().ExecuteAsync(new CreateAppointmentCommand(
            TestFixtures.AdminNorteActor,
            TestFixtures.Pat0101Id,
            TestFixtures.MendozaNorteId,
            "2026-11-20T10:00:00-06:00",
            30,
            "Directory down"));
        Assert.Equal(503, dirDown.Error.HttpStatus);
        Assert.Equal("DIRECTORY_UNAVAILABLE", dirDown.Error.Code);
    }

    [Fact]
    public async Task ListAppointments_EnforcesScopeByRoleAndDateRange()
    {
        SeedAppointment(patientId: TestFixtures.Pat0101Id, specialistId: TestFixtures.MendozaNorteId, startGmt6: "2026-11-20T10:00:00-06:00");
        SeedAppointment(patientId: TestFixtures.Pat0102Id, specialistId: TestFixtures.SalazarNorteId, startGmt6: "2026-11-20T11:00:00-06:00");
        SeedAppointment(patientId: TestFixtures.Pat0103UnassignedId, specialistId: TestFixtures.SalazarNorteId, startGmt6: "2026-11-21T09:00:00-06:00");

        var adminAll = await ListSut().ExecuteAsync(new ListAppointmentsQuery(TestFixtures.AdminNorteActor));
        Assert.Equal(3, adminAll.Value.Total);

        var auxScoped = await ListSut().ExecuteAsync(new ListAppointmentsQuery(TestFixtures.AuxNorteActor));
        Assert.Equal(2, auxScoped.Value.Total);
        Assert.DoesNotContain(auxScoped.Value.Items, x => x.PatientId == TestFixtures.Pat0102Id);

        var mendozaOwn = await ListSut().ExecuteAsync(new ListAppointmentsQuery(TestFixtures.MendozaNorteActor));
        Assert.Single(mendozaOwn.Value.Items);
        Assert.Equal(TestFixtures.MendozaNorteId, mendozaOwn.Value.Items[0].SpecialistId);

        var mendozaOtherSpec = await ListSut().ExecuteAsync(new ListAppointmentsQuery(
            TestFixtures.MendozaNorteActor,
            SpecialistId: TestFixtures.SalazarNorteId));
        Assert.True(mendozaOtherSpec.IsFailure);
        Assert.Equal(403, mendozaOtherSpec.Error.HttpStatus);

        var day20 = await ListSut().ExecuteAsync(new ListAppointmentsQuery(
            TestFixtures.AdminNorteActor,
            FromDateGmt6: "2026-11-20",
            ToDateGmt6: "2026-11-20"));
        Assert.Equal(2, day20.Value.Total);

        var badDates = await ListSut().ExecuteAsync(new ListAppointmentsQuery(
            TestFixtures.AdminNorteActor,
            FromDateGmt6: "2026-11-22",
            ToDateGmt6: "2026-11-20"));
        Assert.True(badDates.IsFailure);
        Assert.Equal("INVALID_DATE_FILTER", badDates.Error.Code);
    }

    [Fact]
    public async Task RescheduleAndTransition_RecordHistoryAndReleaseCancelledSlot()
    {
        var apt = SeedAppointment(patientId: TestFixtures.Pat0101Id, specialistId: TestFixtures.MendozaNorteId, startGmt6: "2026-11-20T10:00:00-06:00", duration: 30);

        var partialShift = await RescheduleSut().ExecuteAsync(new RescheduleAppointmentCommand(
            TestFixtures.AuxNorteActor,
            apt.Id,
            "2026-11-20T10:15:00-06:00",
            45,
            "Ajuste solicitado por paciente"));
        Assert.True(partialShift.IsSuccess);
        Assert.Equal(45, partialShift.Value.DurationMinutes);
        Assert.Equal("2026-11-20T11:00:00-06:00", partialShift.Value.ScheduledEndGmt6);

        var confirmed = await TransitionSut().ExecuteAsync(new TransitionAppointmentStatusCommand(
            TestFixtures.AuxNorteActor,
            apt.Id,
            "CONFIRMADA",
            "Confirmada por teléfono"));
        Assert.True(confirmed.IsSuccess);
        Assert.Equal("CONFIRMADA", confirmed.Value.Status);

        var cancelled = await TransitionSut().ExecuteAsync(new TransitionAppointmentStatusCommand(
            TestFixtures.AuxNorteActor,
            apt.Id,
            "CANCELADA",
            "Cancelación del paciente"));
        Assert.True(cancelled.IsSuccess);

        var rescheduleAfterCancel = await RescheduleSut().ExecuteAsync(new RescheduleAppointmentCommand(
            TestFixtures.AuxNorteActor,
            apt.Id,
            "2026-11-20T14:00:00-06:00",
            30));
        Assert.True(rescheduleAfterCancel.IsFailure);
        Assert.Equal("APPOINTMENT_CLOSED", rescheduleAfterCancel.Error.Code);
        Assert.Equal(409, rescheduleAfterCancel.Error.HttpStatus);

        var invalidAfterCancel = await TransitionSut().ExecuteAsync(new TransitionAppointmentStatusCommand(
            TestFixtures.AuxNorteActor,
            apt.Id,
            "CONFIRMADA"));
        Assert.Equal("INVALID_APPOINTMENT_TRANSITION", invalidAfterCancel.Error.Code);

        var detail = await GetSut().ExecuteAsync(TestFixtures.AdminNorteActor, apt.Id);
        Assert.True(detail.IsSuccess);
        Assert.Equal(4, detail.Value.History.Count);
        Assert.Equal("CREATED", detail.Value.History[0].EventType);
        Assert.Equal("RESCHEDULED", detail.Value.History[1].EventType);
        Assert.Equal("STATUS_CHANGED", detail.Value.History[2].EventType);
        Assert.Equal("STATUS_CHANGED", detail.Value.History[3].EventType);

        var reuseCancelledSlot = await CreateSut().ExecuteAsync(new CreateAppointmentCommand(
            TestFixtures.AuxNorteActor,
            TestFixtures.Pat0101Id,
            TestFixtures.MendozaNorteId,
            "2026-11-20T10:15:00-06:00",
            30,
            "Reutilización de bloque liberado"));
        Assert.True(reuseCancelledSlot.IsSuccess);
    }

    [Fact]
    public async Task GetAppointmentById_IsolatesTenantsAndSpecialists()
    {
        var aptNorteSalazar = SeedAppointment(
            tenantId: TestFixtures.NorteTenantId,
            patientId: TestFixtures.Pat0102Id,
            specialistId: TestFixtures.SalazarNorteId);

        var mendozaForbidden = await GetSut().ExecuteAsync(TestFixtures.MendozaNorteActor, aptNorteSalazar.Id);
        Assert.Equal(403, mendozaForbidden.Error.HttpStatus);
        Assert.Equal("FORBIDDEN_AGENDA_SCOPE", mendozaForbidden.Error.Code);

        var auxForbidden = await GetSut().ExecuteAsync(TestFixtures.AuxNorteActor, aptNorteSalazar.Id);
        Assert.Equal(403, auxForbidden.Error.HttpStatus);
        Assert.Equal("OUT_OF_AGENDA_SCOPE", auxForbidden.Error.Code);

        var crossTenant = await GetSut().ExecuteAsync(TestFixtures.AdminNorteActor, Guid.NewGuid());
        Assert.Equal(404, crossTenant.Error.HttpStatus);
    }
}
