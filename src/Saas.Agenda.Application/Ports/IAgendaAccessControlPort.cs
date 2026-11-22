using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.Application.Ports;

public sealed record AccessControlCheckResult(
    bool ServiceUnavailable,
    bool PatientExists,
    AgendaAccessDecision Decision)
{
    public static AccessControlCheckResult Allowed() =>
        new(false, true, new AgendaAccessDecision(true));

    public static AccessControlCheckResult PatientNotFound() =>
        new(false, false, new AgendaAccessDecision(
            false,
            "PATIENT_NOT_FOUND",
            "El paciente indicado no pertenece a esta clínica."));

    public static AccessControlCheckResult Denied(string code, string message) =>
        new(false, true, new AgendaAccessDecision(false, code, message));

    public static AccessControlCheckResult Unavailable() =>
        new(true, false, new AgendaAccessDecision(
            false,
            "ACCESS_CONTROL_UNAVAILABLE",
            "No fue posible verificar permisos granulares del paciente (CU-10)."));
}

public sealed record AuthorizedPatientsScopeResult(
    bool ServiceUnavailable,
    bool AllowAllTenantPatients,
    IReadOnlySet<Guid> AllowedPatientIds)
{
    private static readonly IReadOnlySet<Guid> EmptySet = new HashSet<Guid>();

    public static AuthorizedPatientsScopeResult AllTenantPatients() =>
        new(false, true, EmptySet);

    public static AuthorizedPatientsScopeResult RestrictedSet(IReadOnlySet<Guid> patientIds) =>
        new(false, false, patientIds);

    public static AuthorizedPatientsScopeResult Unavailable() =>
        new(true, false, EmptySet);
}

public interface IAgendaAccessControlPort
{
    Task<AccessControlCheckResult> EvaluatePatientAccessAsync(
        Guid actorAccountId,
        Guid tenantId,
        OrgRole actorRole,
        Guid patientId,
        AgendaAccessOperation operation,
        CancellationToken cancellationToken = default);

    Task<AuthorizedPatientsScopeResult> GetAuthorizedPatientIdsAsync(
        Guid actorAccountId,
        Guid tenantId,
        OrgRole actorRole,
        CancellationToken cancellationToken = default);
}
