namespace Saas.Agenda.Infrastructure.Directory;

public sealed class SpecialistSeedEntry
{
    public Guid SpecialistId { get; init; }
    public Guid TenantId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? Specialty { get; init; }
    public bool Active { get; init; }
}

public sealed class DirectoryOptions
{
    public const string SectionName = "Directory";

    public string Mode { get; init; } = "Local";
    public bool SimulateUnavailable { get; init; }
    public List<SpecialistSeedEntry> Specialists { get; init; } = [];
}
