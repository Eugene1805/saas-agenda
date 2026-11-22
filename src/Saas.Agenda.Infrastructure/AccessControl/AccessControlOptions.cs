namespace Saas.Agenda.Infrastructure.AccessControl;

public sealed class PatientScopeSeedEntry
{
    public Guid PatientId { get; init; }
    public Guid TenantId { get; init; }
    public Guid? PrimarySpecialistId { get; init; }
    public List<Guid> TreatingSpecialistIds { get; init; } = [];
    public List<Guid> ExceptionalManageAppointmentsAccountIds { get; init; } = [];
}

public sealed class AuxiliaryLinkSeedEntry
{
    public Guid AuxiliaryAccountId { get; init; }
    public Guid TenantId { get; init; }
    public List<Guid> LinkedSpecialistIds { get; init; } = [];
}

public sealed class AccessControlOptions
{
    public const string SectionName = "AccessControl";

    public string Mode { get; init; } = "Local";
    public bool SimulateUnavailable { get; init; }
    public List<PatientScopeSeedEntry> Patients { get; init; } = [];
    public List<AuxiliaryLinkSeedEntry> AuxiliaryLinks { get; init; } = [];
}
