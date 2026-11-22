using Saas.Agenda.Domain.Common;
using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.Application.UseCases.Metrics;

public sealed record AppointmentActivityDataDto(
    int Total,
    IReadOnlyDictionary<string, int> ByStatus);

public sealed record KpiEnvelopeDto(
    string KpiKey,
    string Status,
    AppointmentActivityDataDto Data,
    string EvaluatedAtGmt6,
    string? PeriodStartGmt6,
    string? PeriodEndGmt6);

internal static class MetricsBuilder
{
    public static KpiEnvelopeDto BuildEnvelope(
        string kpiKey,
        IReadOnlyDictionary<AppointmentStatus, int> counts,
        DateTimeOffset utcNow,
        string? periodStartGmt6,
        string? periodEndGmt6)
    {
        var byStatus = new Dictionary<string, int>(StringComparer.Ordinal);
        var total = 0;

        foreach (var status in Enum.GetValues<AppointmentStatus>())
        {
            var count = counts.TryGetValue(status, out var value) ? value : 0;
            byStatus[status.ToString()] = count;
            total += count;
        }

        return new KpiEnvelopeDto(
            kpiKey,
            "AVAILABLE",
            new AppointmentActivityDataDto(total, byStatus),
            Gmt6Clock.FormatIsoGmt6(utcNow),
            periodStartGmt6,
            periodEndGmt6);
    }
}
