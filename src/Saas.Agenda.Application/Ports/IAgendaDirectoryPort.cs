using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.Application.Ports;

public sealed record SpecialistLookupResult(
    bool DirectoryUnavailable,
    SpecialistSummary? Specialist)
{
    public static SpecialistLookupResult Found(SpecialistSummary specialist) => new(false, specialist);
    public static SpecialistLookupResult NotFound() => new(false, null);
    public static SpecialistLookupResult Unavailable() => new(true, null);
}

public interface IAgendaDirectoryPort
{
    Task<SpecialistLookupResult> GetSpecialistAsync(
        Guid tenantId,
        Guid specialistId,
        CancellationToken cancellationToken = default);
}
