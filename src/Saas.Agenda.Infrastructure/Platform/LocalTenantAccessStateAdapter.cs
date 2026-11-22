using Microsoft.Extensions.Options;
using Saas.Agenda.Application.Ports;
using Saas.Agenda.Domain.Common;
using Saas.Agenda.Domain.ValueObjects;
using Saas.Agenda.Infrastructure.Persistence;

namespace Saas.Agenda.Infrastructure.Platform;

internal sealed class LocalTenantAccessStateAdapter(
    IOptions<PlatformOptions> optionsAccessor,
    IClockPort clock) : ITenantAccessStatePort
{
    private static readonly IReadOnlyList<PlatformTenantSeedEntry> DefaultTenants =
    [
        new()
        {
            TenantId = AgendaDbInitializer.NorteTenantId,
            Slug = "clinica-norte",
            Name = "Clínica Médica del Norte",
            State = TenantOperationalState.ACTIVA,
        },
        new()
        {
            TenantId = AgendaDbInitializer.ValleTenantId,
            Slug = "clinica-valle",
            Name = "Consultorios del Valle",
            State = TenantOperationalState.SUSPENDIDA,
        },
        new()
        {
            TenantId = AgendaDbInitializer.AuroraTenantId,
            Slug = "clinica-aurora",
            Name = "Unidad Clínica Aurora",
            State = TenantOperationalState.PENDIENTE_ADMIN,
        },
    ];

    public Task<TenantLookupResult> GetByTenantIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var opts = optionsAccessor.Value;
        if (opts.SimulateUnavailable)
        {
            return Task.FromResult(TenantLookupResult.Unavailable());
        }

        var catalog = opts.Tenants.Count > 0 ? opts.Tenants : DefaultTenants;
        var match = catalog.FirstOrDefault(t => t.TenantId == tenantId);

        if (match is null)
        {
            return Task.FromResult(TenantLookupResult.NotFound());
        }

        var state = new TenantAccessState(
            match.TenantId,
            match.Slug,
            match.Name,
            match.State,
            match.State == TenantOperationalState.ACTIVA,
            Gmt6Clock.FormatIsoGmt6(clock.UtcNow));

        return Task.FromResult(TenantLookupResult.Found(state));
    }
}
