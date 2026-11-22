namespace Saas.Agenda.Domain.ValueObjects;

public sealed record SpecialistSummary(
    Guid SpecialistId,
    Guid TenantId,
    string FullName,
    string? Specialty,
    bool Active);
