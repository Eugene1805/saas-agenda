using Saas.Agenda.Application.Ports;
using Saas.Agenda.Domain.Common;
using Saas.Agenda.Domain.Policies;
using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.Application.UseCases.Schedules;

public sealed class GetSpecialistAvailabilityUseCase(
    IScheduleRepository scheduleRepository,
    IAppointmentRepository appointmentRepository,
    ITenantAccessStatePort tenantAccessStatePort,
    IAgendaDirectoryPort directoryPort,
    IClockPort clock)
{
    public async Task<Result<SpecialistAvailabilityDto>> ExecuteAsync(
        AgendaActorContext actor,
        Guid specialistId,
        string? dateGmt6,
        int durationMinutes = 30,
        CancellationToken cancellationToken = default)
    {
        var tenantCheck = await AgendaUseCaseGuards.RequireActiveClinicActorAsync(
            actor,
            requireWriteRole: false,
            tenantAccessStatePort,
            cancellationToken);

        if (tenantCheck.IsFailure)
        {
            return Result.Failure<SpecialistAvailabilityDto>(tenantCheck.Error);
        }

        if (actor.Role == OrgRole.ESPECIALISTA && actor.AccountId != specialistId)
        {
            return Result.Failure<SpecialistAvailabilityDto>(new DomainError(
                "FORBIDDEN_SCHEDULE_SCOPE",
                "Como especialista solo puedes consultar tu propia disponibilidad horaria.",
                403));
        }

        var tenantId = tenantCheck.Value;
        var specialistCheck = await AgendaUseCaseGuards.ValidateActiveSpecialistAsync(
            tenantId,
            specialistId,
            directoryPort,
            cancellationToken);

        if (specialistCheck.IsFailure)
        {
            return Result.Failure<SpecialistAvailabilityDto>(specialistCheck.Error);
        }

        if (!Gmt6Clock.TryParseGmt6Date(dateGmt6, out var parsedDate))
        {
            return Result.Failure<SpecialistAvailabilityDto>(new DomainError(
                "INVALID_DATE_FILTER",
                "dateGmt6 debe tener formato yyyy-MM-dd en zona horaria GMT-6.",
                400));
        }

        if (durationMinutes < AppointmentPolicy.MinDurationMinutes ||
            durationMinutes > AppointmentPolicy.MaxDurationMinutes)
        {
            return Result.Failure<SpecialistAvailabilityDto>(new DomainError(
                "INVALID_SCHEDULE_TIME",
                $"durationMinutes debe estar entre {AppointmentPolicy.MinDurationMinutes} y {AppointmentPolicy.MaxDurationMinutes} minutos.",
                400));
        }

        var specialistSchedule = await scheduleRepository.GetSpecialistScheduleAsync(tenantId, specialistId, cancellationToken);
        var clinicSchedule = await scheduleRepository.GetClinicScheduleAsync(tenantId, cancellationToken);
        var exceptionForDate = await scheduleRepository.GetSpecialistExceptionByDateAsync(tenantId, specialistId, parsedDate, cancellationToken);

        var effectiveDay = SchedulePolicy.ResolveEffectiveDaySchedule(
            parsedDate,
            specialistSchedule,
            clinicSchedule,
            exceptionForDate);

        var (dayStartUtc, dayEndExclusiveUtc) = Gmt6Clock.ToUtcDayBounds(parsedDate, parsedDate);
        var activeDayAppointments = await appointmentRepository.ListActiveBySpecialistInRangeAsync(
            tenantId,
            specialistId,
            dayStartUtc,
            dayEndExclusiveUtc,
            excludeAppointmentId: null,
            cancellationToken);

        var freeSlots = SchedulePolicy.ComputeAvailableSlots(
            parsedDate,
            effectiveDay,
            activeDayAppointments,
            durationMinutes,
            clock.UtcNow);

        var dayName = ScheduleDayOfWeekExtensions.FromDateOnly(parsedDate).ToString();

        return Result.Success(new SpecialistAvailabilityDto(
            tenantId,
            specialistId,
            Gmt6Clock.FormatDateGmt6(parsedDate),
            durationMinutes,
            effectiveDay.Source.ToString(),
            effectiveDay.IsUnavailable,
            effectiveDay.Reason,
            effectiveDay.Blocks
                .Select(b => new ScheduleSlotDto(
                    dayName,
                    Gmt6Clock.FormatTime(b.StartTime),
                    Gmt6Clock.FormatTime(b.EndTime)))
                .ToArray(),
            activeDayAppointments
                .OrderBy(a => a.ScheduledStartUtc)
                .Select(a => new AvailabilityBusyBlockDto(
                    a.Id,
                    Gmt6Clock.FormatIsoGmt6(a.ScheduledStartUtc),
                    Gmt6Clock.FormatIsoGmt6(a.ScheduledEndUtc),
                    a.Status.ToString()))
                .ToArray(),
            freeSlots
                .Select(s => new AvailabilityFreeSlotDto(
                    Gmt6Clock.FormatTime(s.StartTime),
                    Gmt6Clock.FormatTime(s.EndTime),
                    s.ScheduledStartGmt6,
                    s.ScheduledEndGmt6))
                .ToArray()));
    }
}
