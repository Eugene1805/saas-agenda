using Saas.Agenda.Domain.Common;
using Saas.Agenda.Domain.Entities;
using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.Domain.Policies;

public static class AppointmentPolicy
{
    public const int MinDurationMinutes = 10;
    public const int MaxDurationMinutes = 480;
    public const int MaxReasonLength = 300;
    public const int MaxNoteLength = 500;

    private static readonly IReadOnlyDictionary<AppointmentStatus, AppointmentStatus[]> AllowedTransitionsMap =
        new Dictionary<AppointmentStatus, AppointmentStatus[]>
        {
            [AppointmentStatus.PROGRAMADA] =
            [
                AppointmentStatus.CONFIRMADA,
                AppointmentStatus.EN_CURSO,
                AppointmentStatus.CANCELADA,
                AppointmentStatus.NO_ASISTIO,
            ],
            [AppointmentStatus.CONFIRMADA] =
            [
                AppointmentStatus.EN_CURSO,
                AppointmentStatus.CANCELADA,
                AppointmentStatus.NO_ASISTIO,
            ],
            [AppointmentStatus.EN_CURSO] =
            [
                AppointmentStatus.COMPLETADA,
                AppointmentStatus.CANCELADA,
            ],
            [AppointmentStatus.COMPLETADA] = [],
            [AppointmentStatus.CANCELADA] = [],
            [AppointmentStatus.NO_ASISTIO] = [],
        };

    public static IReadOnlyList<AppointmentStatus> GetAllowedTransitions(AppointmentStatus current) =>
        AllowedTransitionsMap[current];

    public static Result ValidateTenantActive(TenantAccessState? tenant, bool platformUnavailable)
    {
        if (platformUnavailable)
        {
            return Result.Failure(new DomainError(
                "TENANT_STATE_UNAVAILABLE",
                "No fue posible verificar el estado operativo de la clínica.",
                503));
        }

        if (tenant is null)
        {
            return Result.Failure(new DomainError(
                "TENANT_CONTEXT_INVALID",
                "Contexto de clínica no encontrado o inválido.",
                403));
        }

        if (tenant.State == TenantOperationalState.PENDIENTE_ADMIN)
        {
            return Result.Failure(new DomainError(
                "TENANT_PENDING_ADMIN",
                "La clínica está en estado PENDIENTE_ADMIN hasta habilitar su administrador inicial.",
                403));
        }

        if (tenant.State == TenantOperationalState.SUSPENDIDA || !tenant.MayAuthenticate)
        {
            return Result.Failure(new DomainError(
                "TENANT_SUSPENDED",
                "La clínica se encuentra SUSPENDIDA. La agenda permanece conservada sin permitir acceso operativo.",
                403));
        }

        return Result.Success();
    }

    public static Result ValidateTransition(AppointmentStatus current, AppointmentStatus next)
    {
        if (current == next)
        {
            return Result.Failure(new DomainError(
                "SAME_APPOINTMENT_STATUS",
                $"La cita ya se encuentra en estado {current}.",
                409));
        }

        if (!AllowedTransitionsMap[current].Contains(next))
        {
            return Result.Failure(new DomainError(
                "INVALID_APPOINTMENT_TRANSITION",
                $"No es válido pasar una cita de {current} a {next} (CU-18 A3).",
                409));
        }

        return Result.Success();
    }

    public static Result ValidateCanReschedule(Appointment appointment)
    {
        if (appointment.IsTerminal)
        {
            return Result.Failure(new DomainError(
                "APPOINTMENT_CLOSED",
                $"No es posible reprogramar una cita en estado {appointment.Status}.",
                409));
        }

        return Result.Success();
    }

    public static Result<(DateTimeOffset StartUtc, DateTimeOffset EndUtc, string NormalizedReason)> ValidateScheduleInput(
        string? scheduledStartGmt6,
        int durationMinutes,
        string? reason)
    {
        var timeCheck = ValidateScheduleTimeAndDuration(scheduledStartGmt6, durationMinutes);
        if (timeCheck.IsFailure)
        {
            return Result.Failure<(DateTimeOffset, DateTimeOffset, string)>(timeCheck.Error);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure<(DateTimeOffset, DateTimeOffset, string)>(new DomainError(
                "INVALID_APPOINTMENT_DATA",
                "El motivo administrativo de la cita es obligatorio (CU-18).",
                400));
        }

        var normalizedReason = reason.Trim();
        if (normalizedReason.Length > MaxReasonLength)
        {
            return Result.Failure<(DateTimeOffset, DateTimeOffset, string)>(new DomainError(
                "INVALID_APPOINTMENT_DATA",
                $"El motivo administrativo no puede superar {MaxReasonLength} caracteres.",
                400));
        }

        var (startUtc, endUtc) = timeCheck.Value;
        return Result.Success((startUtc, endUtc, normalizedReason));
    }

    public static Result<(DateTimeOffset StartUtc, DateTimeOffset EndUtc)> ValidateScheduleTimeAndDuration(
        string? scheduledStartGmt6,
        int durationMinutes)
    {
        if (!Gmt6Clock.TryParseStrictGmt6(scheduledStartGmt6, out var startUtc))
        {
            return Result.Failure<(DateTimeOffset, DateTimeOffset)>(new DomainError(
                "INVALID_SCHEDULE_TIME",
                "Indica una fecha/hora válida en formato ISO-8601 con zona horaria fija GMT-6 (-06:00).",
                400));
        }

        if (durationMinutes < MinDurationMinutes || durationMinutes > MaxDurationMinutes)
        {
            return Result.Failure<(DateTimeOffset, DateTimeOffset)>(new DomainError(
                "INVALID_SCHEDULE_TIME",
                $"La duración de la cita debe estar entre {MinDurationMinutes} y {MaxDurationMinutes} minutos.",
                400));
        }

        return Result.Success((startUtc, startUtc.AddMinutes(durationMinutes)));
    }

    public static Result ValidateNotInPast(DateTimeOffset startUtc, DateTimeOffset nowUtc)
    {
        if (startUtc.ToUniversalTime() < nowUtc.ToUniversalTime())
        {
            return Result.Failure(new DomainError(
                "PAST_SCHEDULE_NOT_ALLOWED",
                "No es posible programar ni reprogramar citas en fechas u horas pasadas (GMT-6).",
                400));
        }

        return Result.Success();
    }

    public static Result<string?> ValidateOptionalNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return Result.Success<string?>(null);
        }

        var trimmed = note.Trim();
        if (trimmed.Length > MaxNoteLength)
        {
            return Result.Failure<string?>(new DomainError(
                "INVALID_APPOINTMENT_DATA",
                $"La nota de auditoría no puede superar {MaxNoteLength} caracteres.",
                400));
        }

        return Result.Success<string?>(trimmed);
    }

    public static bool HasOverlap(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        IEnumerable<Appointment> specialistAppointments,
        Guid? ignoreAppointmentId = null)
    {
        var normalizedStart = startUtc.ToUniversalTime();
        var normalizedEnd = endUtc.ToUniversalTime();

        return specialistAppointments.Any(apt =>
            apt.BlocksSchedule &&
            (ignoreAppointmentId is null || apt.Id != ignoreAppointmentId.Value) &&
            normalizedStart < apt.ScheduledEndUtc &&
            normalizedEnd > apt.ScheduledStartUtc);
    }
}
