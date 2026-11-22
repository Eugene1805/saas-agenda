using Saas.Agenda.Application.Ports;
using Saas.Agenda.Domain.Common;
using Saas.Agenda.Domain.Policies;

namespace Saas.Agenda.Application.UseCases.Appointments;

public sealed class TransitionAppointmentStatusUseCase(
    IAppointmentRepository appointmentRepository,
    ITenantAccessStatePort tenantAccessStatePort,
    IAgendaAccessControlPort accessControlPort,
    IClockPort clock)
{
    public async Task<Result<AppointmentDto>> ExecuteAsync(
        TransitionAppointmentStatusCommand command,
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

        var statusParse = AgendaUseCaseGuards.ParseAppointmentStatus(command.NextStatus);
        if (statusParse.IsFailure)
        {
            return Result.Failure<AppointmentDto>(statusParse.Error);
        }

        var nextStatus = statusParse.Value;
        var transitionCheck = AppointmentPolicy.ValidateTransition(appointment.Status, nextStatus);
        if (transitionCheck.IsFailure)
        {
            return Result.Failure<AppointmentDto>(transitionCheck.Error);
        }

        var noteValidation = AppointmentPolicy.ValidateOptionalNote(command.Note);
        if (noteValidation.IsFailure)
        {
            return Result.Failure<AppointmentDto>(noteValidation.Error);
        }

        var historyEntry = appointment.TransitionStatus(
            nextStatus,
            command.Actor.AccountId,
            command.Actor.FullName,
            noteValidation.Value,
            clock.UtcNow);

        await appointmentRepository.UpdateWithHistoryAsync(
            appointment,
            historyEntry,
            cancellationToken);

        return Result.Success(AppointmentMapper.ToDto(appointment));
    }
}
