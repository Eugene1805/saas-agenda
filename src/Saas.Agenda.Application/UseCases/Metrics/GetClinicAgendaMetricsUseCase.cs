using Saas.Agenda.Application.Ports;
using Saas.Agenda.Domain.Common;
using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.Application.UseCases.Metrics;

public sealed class GetClinicAgendaMetricsUseCase(
    IAppointmentRepository appointmentRepository,
    ITenantAccessStatePort tenantAccessStatePort,
    IClockPort clock)
{
    public async Task<Result<KpiEnvelopeDto>> ExecuteAsync(
        AgendaActorContext actor,
        string? fromDateGmt6 = null,
        string? toDateGmt6 = null,
        CancellationToken cancellationToken = default)
    {
        if (actor.Role != OrgRole.ADMIN_CLINICA)
        {
            return Result.Failure<KpiEnvelopeDto>(new DomainError(
                "FORBIDDEN_METRICS_ROLE",
                "Solo AdminClinica puede consultar indicadores agregados de la clínica (CU-16).",
                403));
        }

        var tenantCheck = await AgendaUseCaseGuards.RequireActiveClinicActorAsync(
            actor,
            requireWriteRole: false,
            tenantAccessStatePort,
            cancellationToken);

        if (tenantCheck.IsFailure)
        {
            return Result.Failure<KpiEnvelopeDto>(tenantCheck.Error);
        }

        var dateFilter = AgendaUseCaseGuards.ParseDateRangeFilter(fromDateGmt6, toDateGmt6);
        if (dateFilter.IsFailure)
        {
            return Result.Failure<KpiEnvelopeDto>(dateFilter.Error);
        }

        var counts = await appointmentRepository.CountByStatusAsync(
            tenantCheck.Value,
            dateFilter.Value.FromUtc,
            dateFilter.Value.ToExclusiveUtc,
            cancellationToken);

        return Result.Success(MetricsBuilder.BuildEnvelope(
            "AGENDA_ACTIVITY",
            counts,
            clock.UtcNow,
            dateFilter.Value.NormalizedFromDateGmt6,
            dateFilter.Value.NormalizedToDateGmt6));
    }
}
