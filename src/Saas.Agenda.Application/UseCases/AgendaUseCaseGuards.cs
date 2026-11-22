using Saas.Agenda.Application.Ports;
using Saas.Agenda.Domain.Common;
using Saas.Agenda.Domain.Policies;
using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.Application.UseCases;

internal readonly record struct ParsedGmt6DateFilter(
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToExclusiveUtc,
    string? NormalizedFromDateGmt6,
    string? NormalizedToDateGmt6);

internal static class AgendaUseCaseGuards
{
    public static async Task<Result<Guid>> RequireActiveClinicActorAsync(
        AgendaActorContext actor,
        bool requireWriteRole,
        ITenantAccessStatePort tenantAccessStatePort,
        CancellationToken cancellationToken)
    {
        if (actor.Role == OrgRole.SUPER_ADMIN)
        {
            return Result.Failure<Guid>(new DomainError(
                "FORBIDDEN_AGENDA_ROLE",
                "SuperAdmin no accede a la agenda operativa de una clínica por aislamiento multitenant (CU-10 / CU-18).",
                403));
        }

        if (requireWriteRole && actor.Role is not (OrgRole.ADMIN_CLINICA or OrgRole.AUXILIAR))
        {
            return Result.Failure<Guid>(new DomainError(
                "FORBIDDEN_AGENDA_ROLE",
                "Solo AdminClinica o personal auxiliar autorizado programan o modifican citas internas (CU-18).",
                403));
        }

        if (actor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
        {
            return Result.Failure<Guid>(new DomainError(
                "TENANT_CONTEXT_INVALID",
                "La cuenta autenticada no tiene un contexto de clínica válido.",
                403));
        }

        var tenantLookup = await tenantAccessStatePort.GetByTenantIdAsync(tenantId, cancellationToken);
        var tenantCheck = AppointmentPolicy.ValidateTenantActive(tenantLookup.Tenant, tenantLookup.PlatformUnavailable);
        if (tenantCheck.IsFailure)
        {
            return Result.Failure<Guid>(tenantCheck.Error);
        }

        return Result.Success(tenantId);
    }

    public static async Task<Result<Guid>> RequireScheduleEditorAsync(
        AgendaActorContext actor,
        Guid specialistId,
        ITenantAccessStatePort tenantAccessStatePort,
        CancellationToken cancellationToken)
    {
        var tenantCheck = await RequireActiveClinicActorAsync(
            actor,
            requireWriteRole: false,
            tenantAccessStatePort,
            cancellationToken);

        if (tenantCheck.IsFailure)
        {
            return Result.Failure<Guid>(tenantCheck.Error);
        }

        if (actor.Role == OrgRole.AUXILIAR)
        {
            return Result.Failure<Guid>(new DomainError(
                "FORBIDDEN_SCHEDULE_ROLE",
                "Solo AdminClinica o el propio especialista pueden modificar horarios laborales o excepciones.",
                403));
        }

        if (actor.Role == OrgRole.ESPECIALISTA && actor.AccountId != specialistId)
        {
            return Result.Failure<Guid>(new DomainError(
                "FORBIDDEN_SCHEDULE_SCOPE",
                "Como especialista solo puedes configurar tu propio horario laboral y excepciones.",
                403));
        }

        return tenantCheck;
    }

    public static Result<AppointmentStatus> ParseAppointmentStatus(string? rawStatus)
    {
        if (string.IsNullOrWhiteSpace(rawStatus) ||
            int.TryParse(rawStatus.Trim(), out _) ||
            !Enum.TryParse<AppointmentStatus>(rawStatus.Trim(), ignoreCase: true, out var parsed) ||
            !Enum.IsDefined(parsed))
        {
            return Result.Failure<AppointmentStatus>(new DomainError(
                "INVALID_APPOINTMENT_STATUS",
                "El estado de cita indicado no pertenece al catálogo cerrado (PROGRAMADA, CONFIRMADA, EN_CURSO, COMPLETADA, CANCELADA, NO_ASISTIO).",
                400));
        }

        return Result.Success(parsed);
    }

    public static Result<ParsedGmt6DateFilter> ParseDateRangeFilter(
        string? fromDateGmt6,
        string? toDateGmt6)
    {
        DateOnly? fromDate = null;
        DateOnly? toDate = null;

        if (!string.IsNullOrWhiteSpace(fromDateGmt6))
        {
            if (!Gmt6Clock.TryParseGmt6Date(fromDateGmt6, out var parsedFrom))
            {
                return Result.Failure<ParsedGmt6DateFilter>(new DomainError(
                    "INVALID_DATE_FILTER",
                    "fromDateGmt6 debe tener formato yyyy-MM-dd en zona horaria GMT-6.",
                    400));
            }

            fromDate = parsedFrom;
        }

        if (!string.IsNullOrWhiteSpace(toDateGmt6))
        {
            if (!Gmt6Clock.TryParseGmt6Date(toDateGmt6, out var parsedTo))
            {
                return Result.Failure<ParsedGmt6DateFilter>(new DomainError(
                    "INVALID_DATE_FILTER",
                    "toDateGmt6 debe tener formato yyyy-MM-dd en zona horaria GMT-6.",
                    400));
            }

            toDate = parsedTo;
        }

        if (fromDate is DateOnly start && toDate is DateOnly end && start > end)
        {
            return Result.Failure<ParsedGmt6DateFilter>(new DomainError(
                "INVALID_DATE_FILTER",
                "fromDateGmt6 no puede ser posterior a toDateGmt6.",
                400));
        }

        DateTimeOffset? fromUtc = fromDate is DateOnly f
            ? new DateTimeOffset(f.Year, f.Month, f.Day, 0, 0, 0, Gmt6Clock.FixedOffset).ToUniversalTime()
            : null;

        DateTimeOffset? toExclusiveUtc = toDate is DateOnly t
            ? new DateTimeOffset(t.Year, t.Month, t.Day, 0, 0, 0, Gmt6Clock.FixedOffset).AddDays(1).ToUniversalTime()
            : null;

        return Result.Success(new ParsedGmt6DateFilter(
            fromUtc,
            toExclusiveUtc,
            fromDate is DateOnly fn ? Gmt6Clock.FormatDateGmt6(fn) : null,
            toDate is DateOnly tn ? Gmt6Clock.FormatDateGmt6(tn) : null));
    }

    public static async Task<Result> ValidatePatientManageAccessAsync(
        AgendaActorContext actor,
        Guid tenantId,
        Guid patientId,
        IAgendaAccessControlPort accessControlPort,
        CancellationToken cancellationToken)
    {
        var check = await accessControlPort.EvaluatePatientAccessAsync(
            actor.AccountId,
            tenantId,
            actor.Role,
            patientId,
            AgendaAccessOperation.MANAGE_APPOINTMENT,
            cancellationToken);

        if (check.ServiceUnavailable)
        {
            return Result.Failure(new DomainError(
                "ACCESS_CONTROL_UNAVAILABLE",
                "No fue posible verificar permisos granulares del paciente (CU-10).",
                503));
        }

        if (!check.PatientExists)
        {
            return Result.Failure(new DomainError(
                check.Decision.DenialCode ?? "PATIENT_NOT_FOUND",
                check.Decision.DenialMessage ?? "El paciente indicado no pertenece a esta clínica.",
                404));
        }

        if (!check.Decision.Allowed)
        {
            return Result.Failure(new DomainError(
                check.Decision.DenialCode ?? "OUT_OF_AGENDA_SCOPE",
                check.Decision.DenialMessage ?? "No cuentas con ámbito ordinario ni permiso excepcional vigente (MANAGE_APPOINTMENTS) para agendar a este paciente.",
                403));
        }

        return Result.Success();
    }

    public static async Task<Result> ValidateActiveSpecialistAsync(
        Guid tenantId,
        Guid specialistId,
        IAgendaDirectoryPort directoryPort,
        CancellationToken cancellationToken)
    {
        var specialistLookup = await directoryPort.GetSpecialistAsync(tenantId, specialistId, cancellationToken);
        if (specialistLookup.DirectoryUnavailable)
        {
            return Result.Failure(new DomainError(
                "DIRECTORY_UNAVAILABLE",
                "No fue posible validar la vigencia del especialista en el directorio.",
                503));
        }

        if (specialistLookup.Specialist is null ||
            specialistLookup.Specialist.TenantId != tenantId ||
            !specialistLookup.Specialist.Active)
        {
            return Result.Failure(new DomainError(
                "INVALID_SPECIALIST",
                "Selecciona un especialista activo dentro de esta clínica (CU-18 A1).",
                400));
        }

        return Result.Success();
    }

    public static async Task<Result> ValidateSpecialistExistsInTenantAsync(
        Guid tenantId,
        Guid specialistId,
        IAgendaDirectoryPort directoryPort,
        CancellationToken cancellationToken)
    {
        var specialistLookup = await directoryPort.GetSpecialistAsync(tenantId, specialistId, cancellationToken);
        if (specialistLookup.DirectoryUnavailable)
        {
            return Result.Failure(new DomainError(
                "DIRECTORY_UNAVAILABLE",
                "No fue posible validar el especialista en el directorio.",
                503));
        }

        if (specialistLookup.Specialist is null ||
            specialistLookup.Specialist.TenantId != tenantId)
        {
            return Result.Failure(new DomainError(
                "INVALID_SPECIALIST",
                "El especialista indicado no pertenece a esta clínica.",
                400));
        }

        return Result.Success();
    }

    public static async Task<Result> ValidateFitsWorkingHoursAsync(
        Guid tenantId,
        Guid specialistId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        IScheduleRepository scheduleRepository,
        CancellationToken cancellationToken)
    {
        var (startDate, _) = Gmt6Clock.ToGmt6DateAndTime(startUtc);
        var specialistSchedule = await scheduleRepository.GetSpecialistScheduleAsync(tenantId, specialistId, cancellationToken);
        var clinicSchedule = await scheduleRepository.GetClinicScheduleAsync(tenantId, cancellationToken);
        var exceptionForDate = await scheduleRepository.GetSpecialistExceptionByDateAsync(tenantId, specialistId, startDate, cancellationToken);

        var effectiveDay = SchedulePolicy.ResolveEffectiveDaySchedule(
            startDate,
            specialistSchedule,
            clinicSchedule,
            exceptionForDate);

        return SchedulePolicy.ValidateAppointmentFitsSchedule(startUtc, endUtc, effectiveDay);
    }
}
