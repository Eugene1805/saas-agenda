using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.Infrastructure.Platform;

public sealed class PlatformTenantSeedEntry
{
    public Guid TenantId { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public TenantOperationalState State { get; init; }
}

public sealed class PlatformOptions
{
    public const string SectionName = "Platform";

    public string Mode { get; init; } = "Local";
    public bool SimulateUnavailable { get; init; }
    public List<PlatformTenantSeedEntry> Tenants { get; init; } = [];
}
