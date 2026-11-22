using Saas.Agenda.Domain.Common;
using Saas.Agenda.Domain.Entities;
using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.Application.UseCases.Schedules;

public sealed record ScheduleSlotDto(
    string DayOfWeek,
    string StartTime,
    string EndTime);

public sealed record ScheduleSlotInput(
    string DayOfWeek,
    string StartTime,
    string EndTime);

public sealed record ClinicScheduleDto(
    Guid TenantId,
    bool Configured,
    IReadOnlyList<ScheduleSlotDto> Slots,
    string? UpdatedByName,
    string? UpdatedAtGmt6);

public sealed record ScheduleExceptionDto(
    Guid Id,
    Guid SpecialistId,
    string DateGmt6,
    bool IsUnavailable,
    string? CustomStartTime,
    string? CustomEndTime,
    string Reason,
    string CreatedByName,
    string UpdatedAtGmt6);

public sealed record SpecialistScheduleDto(
    Guid TenantId,
    Guid SpecialistId,
    bool Configured,
    bool UseClinicFallback,
    IReadOnlyList<ScheduleSlotDto> OwnSlots,
    IReadOnlyList<ScheduleSlotDto> EffectiveWeeklySlots,
    IReadOnlyList<ScheduleExceptionDto> UpcomingExceptions,
    string? UpdatedByName,
    string? UpdatedAtGmt6);

public sealed record AvailabilityBusyBlockDto(
    Guid AppointmentId,
    string ScheduledStartGmt6,
    string ScheduledEndGmt6,
    string Status);

public sealed record AvailabilityFreeSlotDto(
    string StartTime,
    string EndTime,
    string ScheduledStartGmt6,
    string ScheduledEndGmt6);

public sealed record SpecialistAvailabilityDto(
    Guid TenantId,
    Guid SpecialistId,
    string DateGmt6,
    int DurationMinutes,
    string Source,
    bool IsUnavailable,
    string? Reason,
    IReadOnlyList<ScheduleSlotDto> WorkingBlocks,
    IReadOnlyList<AvailabilityBusyBlockDto> BusyBlocks,
    IReadOnlyList<AvailabilityFreeSlotDto> AvailableSlots);

public sealed record UpdateClinicScheduleCommand(
    AgendaActorContext Actor,
    IReadOnlyList<ScheduleSlotInput> Slots);

public sealed record UpdateSpecialistScheduleCommand(
    AgendaActorContext Actor,
    Guid SpecialistId,
    bool UseClinicFallback,
    IReadOnlyList<ScheduleSlotInput> Slots);

public sealed record UpsertSpecialistExceptionCommand(
    AgendaActorContext Actor,
    Guid SpecialistId,
    string DateGmt6,
    bool IsUnavailable,
    string? CustomStartTime,
    string? CustomEndTime,
    string Reason);

public sealed record DeleteSpecialistExceptionCommand(
    AgendaActorContext Actor,
    Guid SpecialistId,
    Guid ExceptionId);

internal static class ScheduleMapper
{
    public static ClinicScheduleDto ToDto(Guid tenantId, ClinicSchedule? schedule)
    {
        if (schedule is null)
        {
            return new ClinicScheduleDto(tenantId, false, [], null, null);
        }

        var slots = schedule.Slots
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.StartTime)
            .Select(s => new ScheduleSlotDto(
                s.DayOfWeek.ToString(),
                Gmt6Clock.FormatTime(s.StartTime),
                Gmt6Clock.FormatTime(s.EndTime)))
            .ToArray();

        return new ClinicScheduleDto(
            schedule.TenantId,
            true,
            slots,
            schedule.UpdatedByName,
            Gmt6Clock.FormatIsoGmt6(schedule.UpdatedAtUtc));
    }

    public static ScheduleExceptionDto ToDto(SpecialistScheduleException exception) =>
        new(
            exception.Id,
            exception.SpecialistId,
            Gmt6Clock.FormatDateGmt6(exception.DateGmt6),
            exception.IsUnavailable,
            exception.CustomStartTime is TimeOnly cs ? Gmt6Clock.FormatTime(cs) : null,
            exception.CustomEndTime is TimeOnly ce ? Gmt6Clock.FormatTime(ce) : null,
            exception.Reason,
            exception.CreatedByName,
            Gmt6Clock.FormatIsoGmt6(exception.UpdatedAtUtc));

    public static SpecialistScheduleDto ToDto(
        Guid tenantId,
        Guid specialistId,
        SpecialistSchedule? specialistSchedule,
        ClinicSchedule? clinicSchedule,
        IReadOnlyList<SpecialistScheduleException> upcomingExceptions)
    {
        var ownSlots = specialistSchedule?.Slots
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.StartTime)
            .Select(s => new ScheduleSlotDto(
                s.DayOfWeek.ToString(),
                Gmt6Clock.FormatTime(s.StartTime),
                Gmt6Clock.FormatTime(s.EndTime)))
            .ToArray() ?? [];

        var useFallback = specialistSchedule?.UseClinicFallback ?? true;

        var effectiveWeeklySlots = !useFallback
            ? ownSlots
            : (clinicSchedule?.Slots
                .OrderBy(s => s.DayOfWeek)
                .ThenBy(s => s.StartTime)
                .Select(s => new ScheduleSlotDto(
                    s.DayOfWeek.ToString(),
                    Gmt6Clock.FormatTime(s.StartTime),
                    Gmt6Clock.FormatTime(s.EndTime)))
                .ToArray() ?? []);

        var configured = !useFallback || clinicSchedule is not null;

        return new SpecialistScheduleDto(
            tenantId,
            specialistId,
            configured,
            useFallback,
            ownSlots,
            effectiveWeeklySlots,
            upcomingExceptions
                .OrderBy(e => e.DateGmt6)
                .Select(ToDto)
                .ToArray(),
            specialistSchedule?.UpdatedByName ?? clinicSchedule?.UpdatedByName,
            specialistSchedule is not null
                ? Gmt6Clock.FormatIsoGmt6(specialistSchedule.UpdatedAtUtc)
                : (clinicSchedule is not null ? Gmt6Clock.FormatIsoGmt6(clinicSchedule.UpdatedAtUtc) : null));
    }
}
