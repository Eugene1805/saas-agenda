namespace Saas.Agenda.Domain.ValueObjects;

public enum ScheduleSource
{
    EXCEPTION,
    SPECIALIST_SCHEDULE,
    CLINIC_FALLBACK,
    UNCONFIGURED,
}

public sealed record EffectiveDaySchedule(
    DateOnly DateGmt6,
    ScheduleSource Source,
    bool IsUnavailable,
    string? Reason,
    IReadOnlyList<TimeBlock> Blocks);
