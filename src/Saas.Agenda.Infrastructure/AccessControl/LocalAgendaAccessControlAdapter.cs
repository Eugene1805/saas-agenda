using Microsoft.Extensions.Options;
using Saas.Agenda.Application.Ports;
using Saas.Agenda.Domain.ValueObjects;
using Saas.Agenda.Infrastructure.Persistence;

namespace Saas.Agenda.Infrastructure.AccessControl;

internal sealed class LocalAgendaAccessControlAdapter(
    IOptions<AccessControlOptions> optionsAccessor) : IAgendaAccessControlPort
{
    private static readonly IReadOnlyList<PatientScopeSeedEntry> DefaultPatients =
    [
        new()
        {
            PatientId = AgendaDbInitializer.Pat0101Id,
            TenantId = AgendaDbInitializer.NorteTenantId,
            PrimarySpecialistId = AgendaDbInitializer.MendozaNorteAccountId,
            TreatingSpecialistIds = [AgendaDbInitializer.SalazarNorteAccountId],
            ExceptionalManageAppointmentsAccountIds = [],
        },
        new()
        {
            PatientId = AgendaDbInitializer.Pat0102Id,
            TenantId = AgendaDbInitializer.NorteTenantId,
            PrimarySpecialistId = AgendaDbInitializer.SalazarNorteAccountId,
            TreatingSpecialistIds = [],
            ExceptionalManageAppointmentsAccountIds = [],
        },
        new()
        {
            PatientId = AgendaDbInitializer.Pat0103Id,
            TenantId = AgendaDbInitializer.NorteTenantId,
            PrimarySpecialistId = null,
            TreatingSpecialistIds = [],
            ExceptionalManageAppointmentsAccountIds = [],
        },
        new()
        {
            PatientId = AgendaDbInitializer.Pat0104Id,
            TenantId = AgendaDbInitializer.NorteTenantId,
            PrimarySpecialistId = AgendaDbInitializer.SalazarNorteAccountId,
            TreatingSpecialistIds = [],
            ExceptionalManageAppointmentsAccountIds = [AgendaDbInitializer.AuxRamirezNorteAccountId],
        },
        new()
        {
            PatientId = AgendaDbInitializer.Pat0201Id,
            TenantId = AgendaDbInitializer.ValleTenantId,
            PrimarySpecialistId = AgendaDbInitializer.MendozaValleAccountId,
            TreatingSpecialistIds = [],
            ExceptionalManageAppointmentsAccountIds = [],
        },
    ];

    private static readonly IReadOnlyList<AuxiliaryLinkSeedEntry> DefaultAuxiliaryLinks =
    [
        new()
        {
            AuxiliaryAccountId = AgendaDbInitializer.AuxRamirezNorteAccountId,
            TenantId = AgendaDbInitializer.NorteTenantId,
            LinkedSpecialistIds = [AgendaDbInitializer.MendozaNorteAccountId],
        },
    ];

    public Task<AccessControlCheckResult> EvaluatePatientAccessAsync(
        Guid actorAccountId,
        Guid tenantId,
        OrgRole actorRole,
        Guid patientId,
        AgendaAccessOperation operation,
        CancellationToken cancellationToken = default)
    {
        var opts = optionsAccessor.Value;
        if (opts.SimulateUnavailable)
        {
            return Task.FromResult(AccessControlCheckResult.Unavailable());
        }

        var patients = opts.Patients.Count > 0 ? opts.Patients : DefaultPatients;
        var patient = patients.FirstOrDefault(p => p.TenantId == tenantId && p.PatientId == patientId);
        if (patient is null)
        {
            return Task.FromResult(AccessControlCheckResult.PatientNotFound());
        }

        if (actorRole == OrgRole.ADMIN_CLINICA)
        {
            return Task.FromResult(AccessControlCheckResult.Allowed());
        }

        if (actorRole == OrgRole.ESPECIALISTA)
        {
            if (operation == AgendaAccessOperation.MANAGE_APPOINTMENT)
            {
                return Task.FromResult(AccessControlCheckResult.Denied(
                    "FORBIDDEN_AGENDA_ROLE",
                    "Solo AdminClinica o personal auxiliar autorizado programan citas internas (CU-18)."));
            }

            var hasClinicalLink = patient.PrimarySpecialistId == actorAccountId ||
                patient.TreatingSpecialistIds.Contains(actorAccountId);

            return Task.FromResult(hasClinicalLink
                ? AccessControlCheckResult.Allowed()
                : AccessControlCheckResult.Denied(
                    "OUT_OF_CLINICAL_SCOPE",
                    "El paciente no tiene vínculo clínico vigente con este especialista."));
        }

        if (actorRole == OrgRole.AUXILIAR)
        {
            var linkedSpecialists = ResolveLinkedSpecialists(opts, tenantId, actorAccountId);
            if (IsPatientAuthorizedForAuxiliary(patient, actorAccountId, linkedSpecialists))
            {
                return Task.FromResult(AccessControlCheckResult.Allowed());
            }

            return Task.FromResult(AccessControlCheckResult.Denied(
                "OUT_OF_AGENDA_SCOPE",
                "No tienes ámbito ordinario ni permiso excepcional vigente (MANAGE_APPOINTMENTS) para agendar a este paciente."));
        }

        return Task.FromResult(AccessControlCheckResult.Denied(
            "FORBIDDEN_AGENDA_ROLE",
            "Rol no autorizado para operar la agenda clínica."));
    }

    public Task<AuthorizedPatientsScopeResult> GetAuthorizedPatientIdsAsync(
        Guid actorAccountId,
        Guid tenantId,
        OrgRole actorRole,
        CancellationToken cancellationToken = default)
    {
        var opts = optionsAccessor.Value;
        if (opts.SimulateUnavailable)
        {
            return Task.FromResult(AuthorizedPatientsScopeResult.Unavailable());
        }

        if (actorRole == OrgRole.ADMIN_CLINICA)
        {
            return Task.FromResult(AuthorizedPatientsScopeResult.AllTenantPatients());
        }

        var patients = (opts.Patients.Count > 0 ? opts.Patients : DefaultPatients)
            .Where(p => p.TenantId == tenantId);

        if (actorRole == OrgRole.AUXILIAR)
        {
            var linkedSpecialists = ResolveLinkedSpecialists(opts, tenantId, actorAccountId);
            var allowed = patients
                .Where(p => IsPatientAuthorizedForAuxiliary(p, actorAccountId, linkedSpecialists))
                .Select(p => p.PatientId)
                .ToHashSet();

            return Task.FromResult(AuthorizedPatientsScopeResult.RestrictedSet(allowed));
        }

        if (actorRole == OrgRole.ESPECIALISTA)
        {
            var allowed = patients
                .Where(p =>
                    p.PrimarySpecialistId == actorAccountId ||
                    p.TreatingSpecialistIds.Contains(actorAccountId))
                .Select(p => p.PatientId)
                .ToHashSet();

            return Task.FromResult(AuthorizedPatientsScopeResult.RestrictedSet(allowed));
        }

        return Task.FromResult(AuthorizedPatientsScopeResult.RestrictedSet(new HashSet<Guid>()));
    }

    private static HashSet<Guid> ResolveLinkedSpecialists(
        AccessControlOptions options,
        Guid tenantId,
        Guid auxiliaryAccountId)
    {
        var links = options.AuxiliaryLinks.Count > 0 ? options.AuxiliaryLinks : DefaultAuxiliaryLinks;
        var entry = links.FirstOrDefault(l =>
            l.TenantId == tenantId && l.AuxiliaryAccountId == auxiliaryAccountId);

        return entry is null ? [] : entry.LinkedSpecialistIds.ToHashSet();
    }

    private static bool IsPatientAuthorizedForAuxiliary(
        PatientScopeSeedEntry patient,
        Guid auxiliaryAccountId,
        HashSet<Guid> linkedSpecialists)
    {
        if (patient.PrimarySpecialistId is null)
        {
            return true;
        }

        if (patient.PrimarySpecialistId is Guid primary && linkedSpecialists.Contains(primary))
        {
            return true;
        }

        if (patient.TreatingSpecialistIds.Any(linkedSpecialists.Contains))
        {
            return true;
        }

        return patient.ExceptionalManageAppointmentsAccountIds.Contains(auxiliaryAccountId);
    }
}
