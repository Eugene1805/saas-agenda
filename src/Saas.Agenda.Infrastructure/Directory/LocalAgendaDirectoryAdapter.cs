using Microsoft.Extensions.Options;
using Saas.Agenda.Application.Ports;
using Saas.Agenda.Domain.ValueObjects;
using Saas.Agenda.Infrastructure.Persistence;

namespace Saas.Agenda.Infrastructure.Directory;

internal sealed class LocalAgendaDirectoryAdapter(
    IOptions<DirectoryOptions> optionsAccessor) : IAgendaDirectoryPort
{
    private static readonly IReadOnlyList<SpecialistSeedEntry> DefaultSpecialists =
    [
        new()
        {
            SpecialistId = AgendaDbInitializer.MendozaNorteAccountId,
            TenantId = AgendaDbInitializer.NorteTenantId,
            FullName = "Dra. Sofía Mendoza",
            Specialty = "Cardiología",
            Active = true,
        },
        new()
        {
            SpecialistId = AgendaDbInitializer.SalazarNorteAccountId,
            TenantId = AgendaDbInitializer.NorteTenantId,
            FullName = "Dr. Mateo Salazar",
            Specialty = "Medicina General",
            Active = true,
        },
        new()
        {
            SpecialistId = AgendaDbInitializer.VegaNorteAccountId,
            TenantId = AgendaDbInitializer.NorteTenantId,
            FullName = "Dr. Nicolás Vega",
            Specialty = "Pediatría",
            Active = false,
        },
        new()
        {
            SpecialistId = AgendaDbInitializer.MendozaValleAccountId,
            TenantId = AgendaDbInitializer.ValleTenantId,
            FullName = "Dra. Sofía Mendoza (Cuenta Valle)",
            Specialty = "Cardiología",
            Active = true,
        },
    ];

    public Task<SpecialistLookupResult> GetSpecialistAsync(
        Guid tenantId,
        Guid specialistId,
        CancellationToken cancellationToken = default)
    {
        var opts = optionsAccessor.Value;
        if (opts.SimulateUnavailable)
        {
            return Task.FromResult(SpecialistLookupResult.Unavailable());
        }

        var catalog = opts.Specialists.Count > 0 ? opts.Specialists : DefaultSpecialists;
        var match = catalog.FirstOrDefault(s =>
            s.TenantId == tenantId && s.SpecialistId == specialistId);

        if (match is null)
        {
            return Task.FromResult(SpecialistLookupResult.NotFound());
        }

        return Task.FromResult(SpecialistLookupResult.Found(new SpecialistSummary(
            match.SpecialistId,
            match.TenantId,
            match.FullName,
            match.Specialty,
            match.Active)));
    }
}
