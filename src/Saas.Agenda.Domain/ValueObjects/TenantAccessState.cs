namespace Saas.Agenda.Domain.ValueObjects;

public sealed record TenantAccessState(
    Guid TenantId,
    string Slug,
    string Name,
    TenantOperationalState State,
    bool MayAuthenticate,
    string EvaluatedAtGmt6);
