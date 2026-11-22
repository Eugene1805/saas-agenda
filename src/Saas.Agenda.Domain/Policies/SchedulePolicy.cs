using Saas.Agenda.Domain.Common;
using Saas.Agenda.Domain.Entities;
using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.Domain.Policies;

public sealed record ValidatedExceptionInput(
    DateOnly DateGmt6,
    bool IsUnavailable,
    TimeOnly? CustomStartTime,
    TimeOnly? CustomEndTime,
    string NormalizedReason);

public sealed record ComputedFreeSlot(
    TimeOnly StartTime,
    TimeOnly EndTime,
    string ScheduledStartGmt6,
    string ScheduledEndGmt6);

public static class SchedulePolicy
{
    public const int MinBlockMinutes = AppointmentPolicy.MinDurationMinutes;
    public const int MaxExceptionReasonLength = 300;

    public static Result<IReadOnlyList<(ScheduleDayOfWeek Day, TimeOnly Start, TimeOnly End)>> ValidateWeeklySlots(
        IReadOnlyList<(string DayOfWeek, string StartTime, string EndTime)>? rawSlots)
    {
        if (rawSlots is null)
        {
            return Result.Failure<IReadOnlyList<(ScheduleDayOfWeek, TimeOnly, TimeOnly)>>(new DomainError(
                "INVALID_SCHEDULE_BLOCK",
                "Especifica la lista de bloques horarios semanales.",
                400));
        }

        var parsed = new List<(ScheduleDayOfWeek Day, TimeOnly Start, TimeOnly End)>(rawSlots.Count);
        foreach (var raw in rawSlots)
        {
            if (string.IsNullOrWhiteSpace(raw.DayOfWeek) ||
                int.TryParse(raw.DayOfWeek.Trim(), out _) ||
                !Enum.TryParse<ScheduleDayOfWeek>(raw.DayOfWeek.Trim(), ignoreCase: true, out var day) ||
                !Enum.IsDefined(day))
            {
                return Result.Failure<IReadOnlyList<(ScheduleDayOfWeek, TimeOnly, TimeOnly)>>(new DomainError(
                    "INVALID_SCHEDULE_DAY",
                    $"El día '{raw.DayOfWeek}' no es válido (usa MONDAY..SUNDAY).",
                    400));
            }

            if (!Gmt6Clock.TryParseTimeOnly(raw.StartTime, out var start) ||
                !Gmt6Clock.TryParseTimeOnly(raw.EndTime, out var end))
            {
                return Result.Failure<IReadOnlyList<(ScheduleDayOfWeek, TimeOnly, TimeOnly)>>(new DomainError(
                    "INVALID_SCHEDULE_BLOCK",
                    "Cada bloque horario requiere horas válidas en formato HH:mm (GMT-6).",
                    400));
            }

            if (end <= start || (end.ToTimeSpan() - start.ToTimeSpan()).TotalMinutes < MinBlockMinutes)
            {
                return Result.Failure<IReadOnlyList<(ScheduleDayOfWeek, TimeOnly, TimeOnly)>>(new DomainError(
                    "INVALID_SCHEDULE_BLOCK",
                    $"La hora de fin ({raw.EndTime}) debe superar la hora de inicio ({raw.StartTime}) por al menos {MinBlockMinutes} minutos.",
                    400));
            }

            parsed.Add((day, start, end));
        }

        foreach (var group in parsed.GroupBy(x => x.Day))
        {
            var ordered = group.OrderBy(x => x.Start).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].Start < ordered[i - 1].End)
                {
                    return Result.Failure<IReadOnlyList<(ScheduleDayOfWeek, TimeOnly, TimeOnly)>>(new DomainError(
                        "OVERLAPPING_SCHEDULE_BLOCKS",
                        $"Los bloques horarios del día {group.Key} se traslapan entre {Gmt6Clock.FormatTime(ordered[i - 1].Start)}-{Gmt6Clock.FormatTime(ordered[i - 1].End)} y {Gmt6Clock.FormatTime(ordered[i].Start)}-{Gmt6Clock.FormatTime(ordered[i].End)}.",
                        400));
                }
            }
        }

        IReadOnlyList<(ScheduleDayOfWeek, TimeOnly, TimeOnly)> normalized = parsed
            .OrderBy(x => x.Day)
            .ThenBy(x => x.Start)
            .ToArray();

        return Result.Success(normalized);
    }

    public static Result<ValidatedExceptionInput> ValidateExceptionInput(
        string? dateGmt6,
        bool isUnavailable,
        string? customStartTime,
        string? customEndTime,
        string? reason,
        DateOnly todayGmt6)
    {
        if (!Gmt6Clock.TryParseGmt6Date(dateGmt6, out var parsedDate))
        {
            return Result.Failure<ValidatedExceptionInput>(new DomainError(
                "INVALID_DATE_FILTER",
                "dateGmt6 debe tener formato yyyy-MM-dd en zona horaria GMT-6.",
                400));
        }

        if (parsedDate < todayGmt6)
        {
            return Result.Failure<ValidatedExceptionInput>(new DomainError(
                "PAST_SCHEDULE_NOT_ALLOWED",
                "No se permiten excepciones de horario sobre fechas pasadas (GMT-6).",
                400));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure<ValidatedExceptionInput>(new DomainError(
                "INVALID_SCHEDULE_EXCEPTION",
                "Indica el motivo de la excepción o día inhábil.",
                400));
        }

        var normalizedReason = reason.Trim();
        if (normalizedReason.Length > MaxExceptionReasonLength)
        {
            return Result.Failure<ValidatedExceptionInput>(new DomainError(
                "INVALID_SCHEDULE_EXCEPTION",
                $"El motivo de la excepción no puede exceder {MaxExceptionReasonLength} caracteres.",
                400));
        }

        if (isUnavailable)
        {
            if (!string.IsNullOrWhiteSpace(customStartTime) || !string.IsNullOrWhiteSpace(customEndTime))
            {
                return Result.Failure<ValidatedExceptionInput>(new DomainError(
                    "INVALID_SCHEDULE_BLOCK",
                    "Una excepción marcada como día no laborable no debe incluir horas personalizadas.",
                    400));
            }

            return Result.Success(new ValidatedExceptionInput(
                parsedDate,
                true,
                null,
                null,
                normalizedReason));
        }

        if (!Gmt6Clock.TryParseTimeOnly(customStartTime, out var start) ||
            !Gmt6Clock.TryParseTimeOnly(customEndTime, out var end) ||
            end <= start ||
            (end.ToTimeSpan() - start.ToTimeSpan()).TotalMinutes < MinBlockMinutes)
        {
            return Result.Failure<ValidatedExceptionInput>(new DomainError(
                "INVALID_SCHEDULE_BLOCK",
                $"Una excepción habilitada requiere horario válido HH:mm con duración mínima de {MinBlockMinutes} minutos.",
                400));
        }

        return Result.Success(new ValidatedExceptionInput(
            parsedDate,
            false,
            start,
            end,
            normalizedReason));
    }

    public static EffectiveDaySchedule ResolveEffectiveDaySchedule(
        DateOnly dateGmt6,
        SpecialistSchedule? specialistSchedule,
        ClinicSchedule? clinicSchedule,
        SpecialistScheduleException? exceptionForDate)
    {
        if (exceptionForDate is not null)
        {
            if (exceptionForDate.IsUnavailable ||
                exceptionForDate.CustomStartTime is not TimeOnly cs ||
                exceptionForDate.CustomEndTime is not TimeOnly ce)
            {
                return new EffectiveDaySchedule(
                    dateGmt6,
                    ScheduleSource.EXCEPTION,
                    true,
                    exceptionForDate.Reason,
                    []);
            }

            return new EffectiveDaySchedule(
                dateGmt6,
                ScheduleSource.EXCEPTION,
                false,
                exceptionForDate.Reason,
                [new TimeBlock(cs, ce)]);
        }

        var day = ScheduleDayOfWeekExtensions.FromDateOnly(dateGmt6);

        if (specialistSchedule is not null && !specialistSchedule.UseClinicFallback)
        {
            var specBlocks = specialistSchedule.Slots
                .Where(s => s.DayOfWeek == day)
                .OrderBy(s => s.StartTime)
                .Select(s => new TimeBlock(s.StartTime, s.EndTime))
                .ToArray();

            return new EffectiveDaySchedule(
                dateGmt6,
                ScheduleSource.SPECIALIST_SCHEDULE,
                specBlocks.Length == 0,
                specBlocks.Length == 0 ? "Día sin bloques asignados en el horario del especialista." : null,
                specBlocks);
        }

        if (clinicSchedule is not null)
        {
            var clinicBlocks = clinicSchedule.Slots
                .Where(s => s.DayOfWeek == day)
                .OrderBy(s => s.StartTime)
                .Select(s => new TimeBlock(s.StartTime, s.EndTime))
                .ToArray();

            return new EffectiveDaySchedule(
                dateGmt6,
                ScheduleSource.CLINIC_FALLBACK,
                clinicBlocks.Length == 0,
                clinicBlocks.Length == 0 ? "Día no laborable según el horario general de la clínica." : null,
                clinicBlocks);
        }

        return new EffectiveDaySchedule(
            dateGmt6,
            ScheduleSource.UNCONFIGURED,
            true,
            "Ni el especialista ni la clínica cuentan con horario configurado.",
            []);
    }

    public static Result ValidateAppointmentFitsSchedule(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        EffectiveDaySchedule effectiveDay)
    {
        var (startDate, startTime) = Gmt6Clock.ToGmt6DateAndTime(startUtc);
        var (endDate, endTime) = Gmt6Clock.ToGmt6DateAndTime(endUtc);

        if (endDate != startDate)
        {
            return Result.Failure(new DomainError(
                "OUTSIDE_WORKING_HOURS",
                "La cita no puede cruzar la medianoche en zona horaria GMT-6.",
                409));
        }

        if (effectiveDay.Source == ScheduleSource.UNCONFIGURED)
        {
            return Result.Failure(new DomainError(
                "SCHEDULE_NOT_CONFIGURED",
                "No existe horario configurado para la clínica ni para este especialista.",
                409));
        }

        if (effectiveDay.IsUnavailable || effectiveDay.Blocks.Count == 0)
        {
            var reasonDetail = string.IsNullOrWhiteSpace(effectiveDay.Reason)
                ? "Día sin jornada habilitada."
                : effectiveDay.Reason;

            return Result.Failure(new DomainError(
                "OUTSIDE_WORKING_HOURS",
                $"Horario fuera de jornada para el {Gmt6Clock.FormatDateGmt6(startDate)} (GMT-6): {reasonDetail}",
                409));
        }

        var fits = effectiveDay.Blocks.Any(b => startTime >= b.StartTime && endTime <= b.EndTime);
        if (!fits)
        {
            var blocksText = string.Join(
                ", ",
                effectiveDay.Blocks.Select(b => $"{Gmt6Clock.FormatTime(b.StartTime)}-{Gmt6Clock.FormatTime(b.EndTime)}"));

            return Result.Failure(new DomainError(
                "OUTSIDE_WORKING_HOURS",
                $"La cita ({Gmt6Clock.FormatTime(startTime)}-{Gmt6Clock.FormatTime(endTime)} GMT-6) queda fuera de la jornada habilitada ({blocksText}).",
                409));
        }

        return Result.Success();
    }

    public static Result ValidateScheduleChangeAgainstAppointments(
        IEnumerable<Appointment> futureActiveAppointments,
        Func<Appointment, DateOnly, EffectiveDaySchedule> resolveProposedDay,
        DateTimeOffset nowUtc)
    {
        var normalizedNow = nowUtc.ToUniversalTime();
        var activeFuture = futureActiveAppointments
            .Where(a => a.BlocksSchedule && a.ScheduledStartUtc >= normalizedNow)
            .OrderBy(a => a.ScheduledStartUtc)
            .ToArray();

        var conflictingIds = new List<string>();
        var conflictingSlots = new List<string>();

        foreach (var apt in activeFuture)
        {
            var (dateGmt6, startTime) = Gmt6Clock.ToGmt6DateAndTime(apt.ScheduledStartUtc);
            var (_, endTime) = Gmt6Clock.ToGmt6DateAndTime(apt.ScheduledEndUtc);
            var effective = resolveProposedDay(apt, dateGmt6);

            var check = ValidateAppointmentFitsSchedule(apt.ScheduledStartUtc, apt.ScheduledEndUtc, effective);
            if (check.IsFailure)
            {
                conflictingIds.Add(apt.Id.ToString());
                conflictingSlots.Add(
                    $"{Gmt6Clock.FormatDateGmt6(dateGmt6)} {Gmt6Clock.FormatTime(startTime)}-{Gmt6Clock.FormatTime(endTime)} (-06:00)");
            }
        }

        if (conflictingIds.Count > 0)
        {
            var preview = string.Join("; ", conflictingSlots.Take(3));
            var suffix = conflictingSlots.Count > 3 ? $" (+{conflictingSlots.Count - 3} más)" : string.Empty;

            return Result.Failure(new DomainError(
                "SCHEDULE_CHANGE_CONFLICTS_WITH_APPOINTMENTS",
                $"No es posible aplicar el cambio de horario porque {conflictingIds.Count} cita(s) activa(s) futura(s) quedarían fuera de jornada: {preview}{suffix}. Reprograma o cancela esas citas primero.",
                409,
                new Dictionary<string, object?>
                {
                    ["conflictingCount"] = conflictingIds.Count,
                    ["conflictingAppointmentIds"] = conflictingIds,
                    ["conflictingSlotsGmt6"] = conflictingSlots,
                }));
        }

        return Result.Success();
    }

    public static IReadOnlyList<ComputedFreeSlot> ComputeAvailableSlots(
        DateOnly dateGmt6,
        EffectiveDaySchedule effectiveDay,
        IReadOnlyList<Appointment> activeDayAppointments,
        int durationMinutes,
        DateTimeOffset nowUtc)
    {
        if (effectiveDay.IsUnavailable ||
            effectiveDay.Blocks.Count == 0 ||
            durationMinutes < AppointmentPolicy.MinDurationMinutes ||
            durationMinutes > AppointmentPolicy.MaxDurationMinutes)
        {
            return [];
        }

        var normalizedNow = nowUtc.ToUniversalTime();
        var results = new List<ComputedFreeSlot>();

        foreach (var block in effectiveDay.Blocks.OrderBy(b => b.StartTime))
        {
            var startMin = block.StartTime.Hour * 60 + block.StartTime.Minute;
            var endMin = block.EndTime.Hour * 60 + block.EndTime.Minute;

            for (var cursorMin = startMin; cursorMin + durationMinutes <= endMin; cursorMin += durationMinutes)
            {
                var slotStartTime = new TimeOnly(cursorMin / 60, cursorMin % 60);
                var slotEndMin = cursorMin + durationMinutes;
                var slotEndTime = new TimeOnly(slotEndMin / 60, slotEndMin % 60);

                var slotStartUtc = Gmt6Clock.CombineGmt6ToUtc(dateGmt6, slotStartTime);
                var slotEndUtc = slotStartUtc.AddMinutes(durationMinutes);

                if (slotStartUtc < normalizedNow)
                {
                    continue;
                }

                if (AppointmentPolicy.HasOverlap(slotStartUtc, slotEndUtc, activeDayAppointments))
                {
                    continue;
                }

                results.Add(new ComputedFreeSlot(
                    slotStartTime,
                    slotEndTime,
                    Gmt6Clock.FormatIsoGmt6(slotStartUtc),
                    Gmt6Clock.FormatIsoGmt6(slotEndUtc)));
            }
        }

        return results;
    }
}
