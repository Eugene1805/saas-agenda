using Saas.Agenda.Application.Ports;
using Saas.Agenda.Domain.Common;
using Saas.Agenda.Domain.Entities;
using Saas.Agenda.Domain.Policies;
using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.Application.UseCases.Schedules;

public sealed class UpdateSpecialistScheduleUseCase(
    IScheduleRepository scheduleRepository,
    IAppointmentRepository appointmentRepository,
    ITenantAccessStatePort tenantAccessStatePort,
    IAgendaDirectoryPort directoryPort,
    IClockPort clock)
{
    public async Task<Result<SpecialistScheduleDto>> ExecuteAsync(
        UpdateSpecialistScheduleCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantCheck = await AgendaUseCaseGuards.RequireScheduleEditorAsync(
            command.Actor,
            command.SpecialistId,
            tenantAccessStatePort,
            cancellationToken);

        if (tenantCheck.IsFailure)
        {
            return Result.Failure<SpecialistScheduleDto>(tenantCheck.Error);
        }

        var tenantId = tenantCheck.Value;
        var specialistCheck = await AgendaUseCaseGuards.ValidateActiveSpecialistAsync(
            tenantId,
            command.SpecialistId,
            directoryPort,
            cancellationToken);

        if (specialistCheck.IsFailure)
        {
            return Result.Failure<SpecialistScheduleDto>(specialistCheck.Error);
        }

        IReadOnlyList<(ScheduleDayOfWeek Day, TimeOnly Start, TimeOnly End)> normalizedSlots = [];
        if (!command.UseClinicFallback)
        {
            var slotsValidation = SchedulePolicy.ValidateWeeklySlots(
                command.Slots.Select(s => (s.DayOfWeek, s.StartTime, s.EndTime)).ToArray());

            if (slotsValidation.IsFailure)
            {
                return Result.Failure<SpecialistScheduleDto>(slotsValidation.Error);
            }

            normalizedSlots = slotsValidation.Value;
        }

        var utcNow = clock.UtcNow;
        var clinicSchedule = await scheduleRepository.GetClinicScheduleAsync(tenantId, cancellationToken);
        var upcomingExceptions = await scheduleRepository.ListSpecialistExceptionsAsync(
            tenantId,
            command.SpecialistId,
            Gmt6Clock.TodayGmt6(utcNow),
            cancellationToken);

        var futureApts = await appointmentRepository.ListFutureActiveBySpecialistAsync(
            tenantId,
            command.SpecialistId,
            utcNow,
            cancellationToken);

        if (futureApts.Count > 0)
        {
            var (proposedCandidate, _) = SpecialistSchedule.Create(
                Guid.NewGuid(),
                tenantId,
                command.SpecialistId,
                command.UseClinicFallback,
                normalizedSlots,
                command.Actor.AccountId,
                command.Actor.FullName,
                utcNow);

            var exceptionMap = upcomingExceptions.ToDictionary(e => e.DateGmt6);

            var conflictCheck = SchedulePolicy.ValidateScheduleChangeAgainstAppointments(
                futureApts,
                (_, dateGmt6) =>
                {
                    exceptionMap.TryGetValue(dateGmt6, out var ex);
                    return SchedulePolicy.ResolveEffectiveDaySchedule(
                        dateGmt6,
                        proposedCandidate,
                        clinicSchedule,
                        ex);
                },
                utcNow);

            if (conflictCheck.IsFailure)
            {
                return Result.Failure<SpecialistScheduleDto>(conflictCheck.Error);
            }
        }

        var existing = await scheduleRepository.GetSpecialistScheduleAsync(tenantId, command.SpecialistId, cancellationToken);
        SpecialistSchedule target;
        IReadOnlyList<SpecialistScheduleSlot> newSlots;
        var isNew = existing is null;

        if (existing is null)
        {
            (target, newSlots) = SpecialistSchedule.Create(
                Guid.NewGuid(),
                tenantId,
                command.SpecialistId,
                command.UseClinicFallback,
                normalizedSlots,
                command.Actor.AccountId,
                command.Actor.FullName,
                utcNow);
        }
        else
        {
            target = existing;
            newSlots = target.Update(
                command.UseClinicFallback,
                normalizedSlots,
                command.Actor.AccountId,
                command.Actor.FullName,
                utcNow);
        }

        await scheduleRepository.SaveSpecialistScheduleAsync(target, isNew, newSlots, cancellationToken);

        return Result.Success(ScheduleMapper.ToDto(
            tenantId,
            command.SpecialistId,
            target,
            clinicSchedule,
            upcomingExceptions));
    }
}
