using Saas.Agenda.Application.UseCases.Appointments;
using Saas.Agenda.Application.UseCases.Schedules;
using Saas.Agenda.Domain.Common;
using Saas.Agenda.Domain.Entities;
using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.UnitTests;

public sealed class ScheduleUseCasesTests
{
    private readonly InMemoryAppointmentRepository _appointments = new();
    private readonly InMemoryScheduleRepository _schedules = new();
    private readonly StubTenantAccessStatePort _platform = new();
    private readonly StubAgendaDirectoryPort _directory = new();
    private readonly StubAgendaAccessControlPort _access = new();
    private readonly FakeClock _clock = new(DateTimeOffset.Parse("2026-11-19T10:00:00-06:00"));

    public ScheduleUseCasesTests()
    {
        _schedules.SeedDefaultNorteSchedules(_clock.UtcNow);
    }

    private Appointment SeedFutureAppointment(
        Guid specialistId,
        string startGmt6,
        int duration = 30,
        AppointmentStatus? transitionTo = null)
    {
        var (apt, _) = Appointment.Create(
            Guid.NewGuid(),
            TestFixtures.NorteTenantId,
            TestFixtures.Pat0101Id,
            specialistId,
            DateTimeOffset.Parse(startGmt6).ToUniversalTime(),
            duration,
            "Cita futura",
            TestFixtures.AdminNorteId,
            "Lic. Mariana Paredes",
            _clock.UtcNow);

        if (transitionTo is AppointmentStatus target)
        {
            apt.TransitionStatus(target, TestFixtures.AdminNorteId, "Admin", null, _clock.UtcNow);
        }

        _appointments.Items.Add(apt);
        return apt;
    }

    [Fact]
    public async Task SpecialistCanUpdateOwnSchedule_AndIsBlockedFromEditingOtherSpecialists()
    {
        var updateSut = new UpdateSpecialistScheduleUseCase(_schedules, _appointments, _platform, _directory, _clock);
        var getSut = new GetSpecialistScheduleUseCase(_schedules, _platform, _directory, _clock);

        var ownEdit = await updateSut.ExecuteAsync(new UpdateSpecialistScheduleCommand(
            TestFixtures.MendozaNorteActor,
            TestFixtures.MendozaNorteId,
            UseClinicFallback: false,
            [new ScheduleSlotInput("MONDAY", "10:00", "14:00")]));
        Assert.True(ownEdit.IsSuccess);
        Assert.Single(ownEdit.Value.OwnSlots);

        var otherEdit = await updateSut.ExecuteAsync(new UpdateSpecialistScheduleCommand(
            TestFixtures.MendozaNorteActor,
            TestFixtures.SalazarNorteId,
            UseClinicFallback: true,
            []));
        Assert.True(otherEdit.IsFailure);
        Assert.Equal("FORBIDDEN_SCHEDULE_SCOPE", otherEdit.Error.Code);
        Assert.Equal(403, otherEdit.Error.HttpStatus);

        var auxEdit = await updateSut.ExecuteAsync(new UpdateSpecialistScheduleCommand(
            TestFixtures.AuxNorteActor,
            TestFixtures.MendozaNorteId,
            UseClinicFallback: true,
            []));
        Assert.Equal("FORBIDDEN_SCHEDULE_ROLE", auxEdit.Error.Code);
        Assert.Equal(403, auxEdit.Error.HttpStatus);

        var otherRead = await getSut.ExecuteAsync(TestFixtures.MendozaNorteActor, TestFixtures.SalazarNorteId);
        Assert.Equal("FORBIDDEN_SCHEDULE_SCOPE", otherRead.Error.Code);
        Assert.Equal(403, otherRead.Error.HttpStatus);
    }

    [Fact]
    public async Task UpdateSpecialistSchedule_BlocksChangeThatOrphansFutureActiveAppointment()
    {
        var apt = SeedFutureAppointment(TestFixtures.MendozaNorteId, "2026-11-23T17:00:00-06:00", 30);
        SeedFutureAppointment(TestFixtures.MendozaNorteId, "2026-11-23T18:00:00-06:00", 30, AppointmentStatus.CANCELADA);

        var updateSut = new UpdateSpecialistScheduleUseCase(_schedules, _appointments, _platform, _directory, _clock);

        var blocked = await updateSut.ExecuteAsync(new UpdateSpecialistScheduleCommand(
            TestFixtures.MendozaNorteActor,
            TestFixtures.MendozaNorteId,
            UseClinicFallback: false,
            [new ScheduleSlotInput("MONDAY", "09:00", "15:00")]));

        Assert.True(blocked.IsFailure);
        Assert.Equal("SCHEDULE_CHANGE_CONFLICTS_WITH_APPOINTMENTS", blocked.Error.Code);
        Assert.Equal(409, blocked.Error.HttpStatus);
        Assert.Equal(1, blocked.Error.Extensions!["conflictingCount"]);
        var ids = Assert.IsAssignableFrom<IReadOnlyList<string>>(blocked.Error.Extensions["conflictingAppointmentIds"]);
        Assert.Single(ids);
        Assert.Equal(apt.Id.ToString(), ids[0]);

        var validInclude = await updateSut.ExecuteAsync(new UpdateSpecialistScheduleCommand(
            TestFixtures.MendozaNorteActor,
            TestFixtures.MendozaNorteId,
            UseClinicFallback: false,
            [
                new ScheduleSlotInput("MONDAY", "09:00", "15:00"),
                new ScheduleSlotInput("MONDAY", "16:30", "17:45"),
            ]));

        Assert.True(validInclude.IsSuccess);
    }

    [Fact]
    public async Task UpdateClinicSchedule_ChecksOnlySpecialistsUsingFallbackAndRejectsAux()
    {
        // Mendoza usa horario propio -> no se afecta; Salazar usa fallback -> se afecta si creamos cita heredada
        var (salazarFallback, _) = SpecialistSchedule.Create(
            Guid.NewGuid(),
            TestFixtures.NorteTenantId,
            TestFixtures.SalazarNorteId,
            useClinicFallback: true,
            [],
            TestFixtures.AdminNorteId,
            "Admin",
            _clock.UtcNow);
        _schedules.SpecialistSchedules.RemoveAll(s => s.SpecialistId == TestFixtures.SalazarNorteId);
        _schedules.SpecialistSchedules.Add(salazarFallback);

        SeedFutureAppointment(TestFixtures.MendozaNorteId, "2026-11-23T17:30:00-06:00", 30);
        var salazarApt = SeedFutureAppointment(TestFixtures.SalazarNorteId, "2026-11-23T15:00:00-06:00", 30);

        var updateClinicSut = new UpdateClinicScheduleUseCase(_schedules, _appointments, _platform, _clock);

        var blocked = await updateClinicSut.ExecuteAsync(new UpdateClinicScheduleCommand(
            TestFixtures.AdminNorteActor,
            [new ScheduleSlotInput("MONDAY", "08:00", "14:00")]));

        Assert.True(blocked.IsFailure);
        Assert.Equal("SCHEDULE_CHANGE_CONFLICTS_WITH_APPOINTMENTS", blocked.Error.Code);
        var ids = Assert.IsAssignableFrom<IReadOnlyList<string>>(blocked.Error.Extensions!["conflictingAppointmentIds"]);
        Assert.Equal([salazarApt.Id.ToString()], ids);

        var auxBlocked = await updateClinicSut.ExecuteAsync(new UpdateClinicScheduleCommand(
            TestFixtures.AuxNorteActor,
            [new ScheduleSlotInput("MONDAY", "08:00", "18:00")]));
        Assert.Equal(403, auxBlocked.Error.HttpStatus);
        Assert.Equal("FORBIDDEN_SCHEDULE_ROLE", auxBlocked.Error.Code);
    }

    [Fact]
    public async Task Exceptions_BlockOrAllowAppointmentsAndPreventOrphanDeletion()
    {
        var upsertSut = new UpsertSpecialistExceptionUseCase(_schedules, _appointments, _platform, _directory, _clock);
        var deleteSut = new DeleteSpecialistExceptionUseCase(_schedules, _appointments, _platform, _directory, _clock);
        var createAptSut = new CreateAppointmentUseCase(_appointments, _schedules, _platform, _directory, _access, _clock);
        var transitionSut = new TransitionAppointmentStatusUseCase(_appointments, _platform, _access, _clock);

        // Domingo 2026-11-22: Mendoza no trabaja ordinariamente, agregamos guardia especial 10:00-13:00
        var specialSunday = await upsertSut.ExecuteAsync(new UpsertSpecialistExceptionCommand(
            TestFixtures.MendozaNorteActor,
            TestFixtures.MendozaNorteId,
            "2026-11-22",
            IsUnavailable: false,
            CustomStartTime: "10:00",
            CustomEndTime: "13:00",
            Reason: "Guardia especial dominical"));
        Assert.True(specialSunday.IsSuccess);

        var aptOnSunday = await createAptSut.ExecuteAsync(new CreateAppointmentCommand(
            TestFixtures.AdminNorteActor,
            TestFixtures.Pat0101Id,
            TestFixtures.MendozaNorteId,
            "2026-11-22T10:30:00-06:00",
            30,
            "Consulta en guardia dominical"));
        Assert.True(aptOnSunday.IsSuccess);

        // Intentar marcar ese domingo como no disponible o eliminar la excepción debe fallar 409
        var blockUnavailable = await upsertSut.ExecuteAsync(new UpsertSpecialistExceptionCommand(
            TestFixtures.MendozaNorteActor,
            TestFixtures.MendozaNorteId,
            "2026-11-22",
            IsUnavailable: true,
            CustomStartTime: null,
            CustomEndTime: null,
            Reason: "Cambio de último momento"));
        Assert.Equal("SCHEDULE_CHANGE_CONFLICTS_WITH_APPOINTMENTS", blockUnavailable.Error.Code);

        var deleteBlocked = await deleteSut.ExecuteAsync(new DeleteSpecialistExceptionCommand(
            TestFixtures.MendozaNorteActor,
            TestFixtures.MendozaNorteId,
            specialSunday.Value.Id));
        Assert.Equal("SCHEDULE_CHANGE_CONFLICTS_WITH_APPOINTMENTS", deleteBlocked.Error.Code);

        // Cancelar cita y ahora sí eliminar excepción funciona
        await transitionSut.ExecuteAsync(new TransitionAppointmentStatusCommand(
            TestFixtures.AdminNorteActor,
            aptOnSunday.Value.Id,
            "CANCELADA",
            "Reprogramada"));

        var deleteOk = await deleteSut.ExecuteAsync(new DeleteSpecialistExceptionCommand(
            TestFixtures.MendozaNorteActor,
            TestFixtures.MendozaNorteId,
            specialSunday.Value.Id));
        Assert.True(deleteOk.IsSuccess);
    }

    [Fact]
    public async Task GetSpecialistAvailability_ComputesFreeAndBusyBlocksWithoutPii()
    {
        SeedFutureAppointment(TestFixtures.MendozaNorteId, "2026-11-20T10:00:00-06:00", 30);

        var sut = new GetSpecialistAvailabilityUseCase(_schedules, _appointments, _platform, _directory, _clock);
        var result = await sut.ExecuteAsync(TestFixtures.AuxNorteActor, TestFixtures.MendozaNorteId, "2026-11-20", 30);

        Assert.True(result.IsSuccess);
        Assert.Equal("SPECIALIST_SCHEDULE", result.Value.Source);
        Assert.Equal(2, result.Value.WorkingBlocks.Count);
        Assert.Single(result.Value.BusyBlocks);
        Assert.DoesNotContain(result.Value.AvailableSlots, s => s.StartTime == "10:00");
        Assert.Contains(result.Value.AvailableSlots, s => s.StartTime == "09:30");
        Assert.Contains(result.Value.AvailableSlots, s => s.StartTime == "10:30");
        Assert.DoesNotContain(result.Value.AvailableSlots, s => s.StartTime == "15:00");
        Assert.Contains(result.Value.AvailableSlots, s => s.StartTime == "16:00");
    }
}
