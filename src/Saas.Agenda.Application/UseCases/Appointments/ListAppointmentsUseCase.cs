using Saas.Agenda.Application.Ports;
using Saas.Agenda.Domain.Common;
using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.Application.UseCases.Appointments;

public sealed class ListAppointmentsUseCase(
    IAppointmentRepository appointmentRepository,
    ITenantAccessStatePort tenantAccessStatePort,
    IAgendaAccessControlPort accessControlPort)
{
    public async Task<Result<AppointmentListResponse>> ExecuteAsync(
        ListAppointmentsQuery query,
        CancellationToken cancellationToken = default)
    {
        var tenantCheck = await AgendaUseCaseGuards.RequireActiveClinicActorAsync(
            query.Actor,
            requireWriteRole: false,
            tenantAccessStatePort,
            cancellationToken);

        if (tenantCheck.IsFailure)
        {
            return Result.Failure<AppointmentListResponse>(tenantCheck.Error);
        }

        var tenantId = tenantCheck.Value;

        AppointmentStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var statusParse = AgendaUseCaseGuards.ParseAppointmentStatus(query.Status);
            if (statusParse.IsFailure)
            {
                return Result.Failure<AppointmentListResponse>(statusParse.Error);
            }

            statusFilter = statusParse.Value;
        }

        var dateFilter = AgendaUseCaseGuards.ParseDateRangeFilter(query.FromDateGmt6, query.ToDateGmt6);
        if (dateFilter.IsFailure)
        {
            return Result.Failure<AppointmentListResponse>(dateFilter.Error);
        }

        var effectiveSpecialistId = query.SpecialistId;
        if (query.Actor.Role == OrgRole.ESPECIALISTA)
        {
            if (query.SpecialistId is Guid requestedSpecialist && requestedSpecialist != query.Actor.AccountId)
            {
                return Result.Failure<AppointmentListResponse>(new DomainError(
                    "FORBIDDEN_AGENDA_SCOPE",
                    "Como especialista solo puedes consultar tu propia agenda de citas.",
                    403));
            }

            effectiveSpecialistId = query.Actor.AccountId;
        }

        IReadOnlySet<Guid>? restrictedPatients = null;
        if (query.Actor.Role == OrgRole.AUXILIAR)
        {
            var scope = await accessControlPort.GetAuthorizedPatientIdsAsync(
                query.Actor.AccountId,
                tenantId,
                query.Actor.Role,
                cancellationToken);

            if (scope.ServiceUnavailable)
            {
                return Result.Failure<AppointmentListResponse>(new DomainError(
                    "ACCESS_CONTROL_UNAVAILABLE",
                    "No fue posible verificar permisos granulares del paciente (CU-10).",
                    503));
            }

            if (!scope.AllowAllTenantPatients)
            {
                restrictedPatients = scope.AllowedPatientIds;
            }
        }

        var items = await appointmentRepository.ListAsync(
            new AppointmentQueryFilter(
                tenantId,
                effectiveSpecialistId,
                query.PatientId,
                statusFilter,
                dateFilter.Value.FromUtc,
                dateFilter.Value.ToExclusiveUtc,
                restrictedPatients),
            cancellationToken);

        var dtos = items.Select(AppointmentMapper.ToDto).ToArray();
        return Result.Success(new AppointmentListResponse(dtos.Length, dtos));
    }
}
