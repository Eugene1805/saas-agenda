using Saas.Agenda.Application.Ports;
using Saas.Agenda.Domain.Common;
using Saas.Agenda.Domain.Policies;

namespace Saas.Agenda.Application.UseCases.Appointments;

public sealed class RescheduleAppointmentUseCase(
    IAppointmentRepository appointmentRepository,
    IScheduleRepository scheduleRepository,
    ITenantAccessStatePort tenantAccessStatePort,
    IAgendaDirectoryPort directoryPort,
    IAgendaAccessControlPort accessControlPort,
    IClockPort clock)
{
    public async Task<Result<AppointmentDto>> ExecuteAsync(
        RescheduleAppointmentCommand command,
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
        var appointment = await appointmentRepository.GetByIdAsync(
            tenantId,
            command.AppointmentId,
            includeHistory: false,
            cancellationToken);

        if (appointment is null)
        {
            return Result.Failure<AppointmentDto>(new DomainError(
                "APPOINTMENT_NOT_FOUND",
                "Cita no encontrada dentro de esta clínica.",
                404));
        }

        var accessCheck = await AgendaUseCaseGuards.ValidatePatientManageAccessAsync(
            command.Actor,
            tenantId,
            appointment.PatientId,
            accessControlPort,
            cancellationToken);

        if (accessCheck.IsFailure)
        {
            return Result.Failure<AppointmentDto>(accessCheck.Error);
        }

        var specialistCheck = await AgendaUseCaseGuards.ValidateActiveSpecialistAsync(
            tenantId,
            appointment.SpecialistId,
            directoryPort,
            cancellationToken);

        if (specialistCheck.IsFailure)
        {
            return Result.Failure<AppointmentDto>(specialistCheck.Error);
        }

        var canReschedule = AppointmentPolicy.ValidateCanReschedule(appointment);
        if (canReschedule.IsFailure)
        {
            return Result.Failure<AppointmentDto>(canReschedule.Error);
        }

        var timeValidation = AppointmentPolicy.ValidateScheduleTimeAndDuration(
            command.ScheduledStartGmt6,
            command.DurationMinutes);

        if (timeValidation.IsFailure)
        {
            return Result.Failure<AppointmentDto>(timeValidation.Error);
        }

        var (newStartUtc, newEndUtc) = timeValidation.Value;
        var utcNow = clock.UtcNow;

        var pastCheck = AppointmentPolicy.ValidateNotInPast(newStartUtc, utcNow);
        if (pastCheck.IsFailure)
        {
            return Result.Failure<AppointmentDto>(pastCheck.Error);
        }

        var noteValidation = AppointmentPolicy.ValidateOptionalNote(command.Note);
        if (noteValidation.IsFailure)
        {
            return Result.Failure<AppointmentDto>(noteValidation.Error);
        }

        var scheduleCheck = await AgendaUseCaseGuards.ValidateFitsWorkingHoursAsync(
            tenantId,
            appointment.SpecialistId,
            newStartUtc,
            newEndUtc,
            scheduleRepository,
            cancellationToken);

        if (scheduleCheck.IsFailure)
        {
            return Result.Failure<AppointmentDto>(scheduleCheck.Error);
        }

        var overlappingCandidates = await appointmentRepository.ListActiveBySpecialistInRangeAsync(
            tenantId,
            appointment.SpecialistId,
            newStartUtc,
            newEndUtc,
            excludeAppointmentId: appointment.Id,
            cancellationToken);

        if (AppointmentPolicy.HasOverlap(newStartUtc, newEndUtc, overlappingCandidates, appointment.Id))
        {
            return Result.Failure<AppointmentDto>(new DomainError(
                "SCHEDULE_CONFLICT",
                "Conflicto de horario en GMT-6: el especialista ya tiene una cita activa en ese bloque.",
                409));
        }

        var historyEntry = appointment.Reschedule(
            newStartUtc,
            command.DurationMinutes,
            command.Actor.AccountId,
            command.Actor.FullName,
            noteValidation.Value,
            utcNow);

        var writeOutcome = await appointmentRepository.UpdateWithHistoryAsync(
            appointment,
            historyEntry,
            cancellationToken);

        if (writeOutcome == RepositoryWriteOutcome.ScheduleConflict)
        {
            return Result.Failure<AppointmentDto>(new DomainError(
                "SCHEDULE_CONFLICT",
                "Conflicto concurrente de horario en GMT-6 al reprogramar la cita.",
                409));
        }

        return Result.Success(AppointmentMapper.ToDto(appointment));
    }
}
