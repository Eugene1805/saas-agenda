using Saas.Agenda.Application.Ports;
using Saas.Agenda.Domain.Common;
using Saas.Agenda.Domain.Entities;
using Saas.Agenda.Domain.Policies;

namespace Saas.Agenda.Application.UseCases.Appointments;

public sealed class CreateAppointmentUseCase(
    IAppointmentRepository appointmentRepository,
    IScheduleRepository scheduleRepository,
    ITenantAccessStatePort tenantAccessStatePort,
    IAgendaDirectoryPort directoryPort,
    IAgendaAccessControlPort accessControlPort,
    IClockPort clock)
{
    public async Task<Result<AppointmentDto>> ExecuteAsync(
        CreateAppointmentCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantCheck = await AgendaUseCaseGuards.RequireActiveClinicActorAsync(
            command.Actor,
            requireWriteRole: true,
            tenantAccessStatePort,
            cancellationToken);

        if (tenantCheck.IsFailure)
        {
            return Result.Failure<AppointmentDto>(tenantCheck.Error);
        }

        var tenantId = tenantCheck.Value;

        var accessCheck = await AgendaUseCaseGuards.ValidatePatientManageAccessAsync(
            command.Actor,
            tenantId,
            command.PatientId,
            accessControlPort,
            cancellationToken);

        if (accessCheck.IsFailure)
        {
            return Result.Failure<AppointmentDto>(accessCheck.Error);
        }

        var specialistCheck = await AgendaUseCaseGuards.ValidateActiveSpecialistAsync(
            tenantId,
            command.SpecialistId,
            directoryPort,
            cancellationToken);

        if (specialistCheck.IsFailure)
        {
            return Result.Failure<AppointmentDto>(specialistCheck.Error);
        }

        var inputValidation = AppointmentPolicy.ValidateScheduleInput(
            command.ScheduledStartGmt6,
            command.DurationMinutes,
            command.Reason);

        if (inputValidation.IsFailure)
        {
            return Result.Failure<AppointmentDto>(inputValidation.Error);
        }

        var (startUtc, endUtc, normalizedReason) = inputValidation.Value;
        var utcNow = clock.UtcNow;

        var pastCheck = AppointmentPolicy.ValidateNotInPast(startUtc, utcNow);
        if (pastCheck.IsFailure)
        {
            return Result.Failure<AppointmentDto>(pastCheck.Error);
        }

        var scheduleCheck = await AgendaUseCaseGuards.ValidateFitsWorkingHoursAsync(
            tenantId,
            command.SpecialistId,
            startUtc,
            endUtc,
            scheduleRepository,
            cancellationToken);

        if (scheduleCheck.IsFailure)
        {
            return Result.Failure<AppointmentDto>(scheduleCheck.Error);
        }

        var overlappingCandidates = await appointmentRepository.ListActiveBySpecialistInRangeAsync(
            tenantId,
            command.SpecialistId,
            startUtc,
            endUtc,
            excludeAppointmentId: null,
            cancellationToken);

        if (AppointmentPolicy.HasOverlap(startUtc, endUtc, overlappingCandidates))
        {
            return Result.Failure<AppointmentDto>(new DomainError(
                "SCHEDULE_CONFLICT",
                "Conflicto de horario en GMT-6: el especialista ya tiene una cita activa en ese bloque.",
                409));
        }

        var (appointment, initialHistory) = Appointment.Create(
            Guid.NewGuid(),
            tenantId,
            command.PatientId,
            command.SpecialistId,
            startUtc,
            command.DurationMinutes,
            normalizedReason,
            command.Actor.AccountId,
            command.Actor.FullName,
            utcNow,
            creationNote: "Cita programada en estado inicial PROGRAMADA (CU-18).");

        var writeOutcome = await appointmentRepository.AddAsync(
            appointment,
            initialHistory,
            cancellationToken);

        if (writeOutcome == RepositoryWriteOutcome.ScheduleConflict)
        {
            return Result.Failure<AppointmentDto>(new DomainError(
                "SCHEDULE_CONFLICT",
                "Conflicto concurrente de horario en GMT-6: otro usuario reservó el bloque del especialista.",
                409));
        }

        return Result.Success(AppointmentMapper.ToDto(appointment));
    }
}
