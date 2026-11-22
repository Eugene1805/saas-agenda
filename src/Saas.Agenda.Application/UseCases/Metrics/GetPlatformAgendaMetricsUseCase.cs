using Saas.Agenda.Application.Ports;
using Saas.Agenda.Domain.Common;
using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.Application.UseCases.Metrics;

public sealed class GetPlatformAgendaMetricsUseCase(
    IAppointmentRepository appointmentRepository,
    IClockPort clock)
{
    public async Task<Result<KpiEnvelopeDto>> ExecuteAsync(
        AgendaActorContext actor,
        string? fromDateGmt6 = null,
        string? toDateGmt6 = null,
        CancellationToken cancellationToken = default)
    {
        if (actor.Role != OrgRole.SUPER_ADMIN)
        {
            return Result.Failure<KpiEnvelopeDto>(new DomainError(
                "FORBIDDEN_METRICS_ROLE",
                "Solo SuperAdmin puede consultar la actividad agregada de citas en plataforma (CU-16).",
                403));
        }

        var dateFilter = AgendaUseCaseGuards.ParseDateRangeFilter(fromDateGmt6, toDateGmt6);
        if (dateFilter.IsFailure)
        {
            return Result.Failure<KpiEnvelopeDto>(dateFilter.Error);
        }

        var counts = await appointmentRepository.CountByStatusAsync(
            tenantId: null,
            dateFilter.Value.FromUtc,
            dateFilter.Value.ToExclusiveUtc,
            cancellationToken);

        return Result.Success(MetricsBuilder.BuildEnvelope(
            "APPOINTMENT_ACTIVITY",
            counts,
            clock.UtcNow,
            dateFilter.Value.NormalizedFromDateGmt6,
            dateFilter.Value.NormalizedToDateGmt6));
    }
}
