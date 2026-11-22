namespace Saas.Agenda.Domain.ValueObjects;

public sealed record AgendaActorContext(
    Guid AccountId,
    Guid? TenantId,
    OrgRole Role,
    string FullName);
