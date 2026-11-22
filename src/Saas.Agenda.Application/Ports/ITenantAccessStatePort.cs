using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.Application.Ports;

public sealed record TenantLookupResult(
    bool PlatformUnavailable,
    TenantAccessState? Tenant)
{
    public static TenantLookupResult Found(TenantAccessState tenant) => new(false, tenant);
    public static TenantLookupResult NotFound() => new(false, null);
    public static TenantLookupResult Unavailable() => new(true, null);
}

public interface ITenantAccessStatePort
{
    Task<TenantLookupResult> GetByTenantIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
