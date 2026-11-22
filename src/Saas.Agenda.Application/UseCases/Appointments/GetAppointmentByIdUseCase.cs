using Saas.Agenda.Application.Ports;
using Saas.Agenda.Domain.Common;
using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.Application.UseCases.Appointments;

public sealed class GetAppointmentByIdUseCase(
    IAppointmentRepository appointmentRepository,
    ITenantAccessStatePort tenantAccessStatePort,
    IAgendaAccessControlPort accessControlPort)
{
    public async Task<Result<AppointmentDetailDto>> ExecuteAsync(
        AgendaActorContext actor,
        Guid appointmentId,
        CancellationToken cancellationToken = default)
    {
        var tenantCheck = await AgendaUseCaseGuards.RequireActiveClinicActorAsync(
            actor,
            requireWriteRole: false,
            tenantAccessStatePort,
            cancellationToken);

        if (tenantCheck.IsFailure)
        {
            return Result.Failure<AppointmentDetailDto>(tenantCheck.Error);
        }

        var tenantId = tenantCheck.Value;
        var appointment = await appointmentRepository.GetByIdAsync(
            tenantId,
            appointmentId,
            includeHistory: true,
            cancellationToken);

        if (appointment is null)
        {
            return Result.Failure<AppointmentDetailDto>(new DomainError(
                "APPOINTMENT_NOT_FOUND",
                "Cita no encontrada dentro de esta clínica.",
                404));
        }

        if (actor.Role == OrgRole.ESPECIALISTA && appointment.SpecialistId != actor.AccountId)
        {
            return Result.Failure<AppointmentDetailDto>(new DomainError(
                "FORBIDDEN_AGENDA_SCOPE",
                "Como especialista solo puedes consultar el detalle de tus propias citas.",
                403));
        }

        if (actor.Role == OrgRole.AUXILIAR)
        {
            var access = await accessControlPort.EvaluatePatientAccessAsync(
                actor.AccountId,
                tenantId,
                actor.Role,
                appointment.PatientId,
                AgendaAccessOperation.READ_APPOINTMENT,
                cancellationToken);

            if (access.ServiceUnavailable)
            {
                return Result.Failure<AppointmentDetailDto>(new DomainError(
                    "ACCESS_CONTROL_UNAVAILABLE",
                    "No fue posible verificar permisos granulares del paciente (CU-10).",
                    503));
            }

            if (!access.PatientExists || !access.Decision.Allowed)
            {
                return Result.Failure<AppointmentDetailDto>(new DomainError(
                    access.Decision.DenialCode ?? "OUT_OF_AGENDA_SCOPE",
                    access.Decision.DenialMessage ?? "Esta cita queda fuera de tu ámbito autorizado (CU-10).",
                    403));
            }
        }

        return Result.Success(AppointmentMapper.ToDetailDto(appointment));
    }
}
