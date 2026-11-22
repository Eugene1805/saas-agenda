using Saas.Agenda.Application.Ports;
using Saas.Agenda.Domain.Common;
using Saas.Agenda.Domain.Entities;
using Saas.Agenda.Domain.Policies;
using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.Application.UseCases.Schedules;

public sealed class UpdateClinicScheduleUseCase(
    IScheduleRepository scheduleRepository,
    IAppointmentRepository appointmentRepository,
    ITenantAccessStatePort tenantAccessStatePort,
    IClockPort clock)
{
    public async Task<Result<ClinicScheduleDto>> ExecuteAsync(
        UpdateClinicScheduleCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantCheck = await AgendaUseCaseGuards.RequireActiveClinicActorAsync(
            command.Actor,
            requireWriteRole: false,
            tenantAccessStatePort,
            cancellationToken);

        if (tenantCheck.IsFailure)
        {
            return Result.Failure<ClinicScheduleDto>(tenantCheck.Error);
        }

        if (command.Actor.Role != OrgRole.ADMIN_CLINICA)
        {
            return Result.Failure<ClinicScheduleDto>(new DomainError(
                "FORBIDDEN_SCHEDULE_ROLE",
                "Solo AdminClinica puede configurar el horario general de la clínica.",
                403));
        }

        var slotsValidation = SchedulePolicy.ValidateWeeklySlots(
            command.Slots.Select(s => (s.DayOfWeek, s.StartTime, s.EndTime)).ToArray());

        if (slotsValidation.IsFailure)
        {
            return Result.Failure<ClinicScheduleDto>(slotsValidation.Error);
        }

        var tenantId = tenantCheck.Value;
        var utcNow = clock.UtcNow;
        var (proposedCandidate, _) = ClinicSchedule.Create(
            tenantId,
            slotsValidation.Value,
            command.Actor.AccountId,
            command.Actor.FullName,
            utcNow);

        var customSpecialistIds = await scheduleRepository.ListCustomSpecialistIdsAsync(tenantId, cancellationToken);
        var inheritedFutureApts = await appointmentRepository.ListFutureActiveByTenantExcludingSpecialistsAsync(
            tenantId,
            customSpecialistIds,
            utcNow,
            cancellationToken);

        if (inheritedFutureApts.Count > 0)
        {
            var exceptionsBySpecialist = new Dictionary<Guid, Dictionary<DateOnly, SpecialistScheduleException>>();
            var todayGmt6 = Gmt6Clock.TodayGmt6(utcNow);

            foreach (var specId in inheritedFutureApts.Select(a => a.SpecialistId).Distinct())
            {
                var exList = await scheduleRepository.ListSpecialistExceptionsAsync(tenantId, specId, todayGmt6, cancellationToken);
                exceptionsBySpecialist[specId] = exList.ToDictionary(e => e.DateGmt6);
            }

            var conflictCheck = SchedulePolicy.ValidateScheduleChangeAgainstAppointments(
                inheritedFutureApts,
                (apt, dateGmt6) =>
                {
                    SpecialistScheduleException? ex = null;
                    if (exceptionsBySpecialist.TryGetValue(apt.SpecialistId, out var map))
                    {
                        map.TryGetValue(dateGmt6, out ex);
                    }

                    return SchedulePolicy.ResolveEffectiveDaySchedule(
                        dateGmt6,
                        specialistSchedule: null,
                        proposedCandidate,
                        ex);
                },
                utcNow);

            if (conflictCheck.IsFailure)
            {
                return Result.Failure<ClinicScheduleDto>(conflictCheck.Error);
            }
        }

        var existing = await scheduleRepository.GetClinicScheduleAsync(tenantId, cancellationToken);
        ClinicSchedule target;
        IReadOnlyList<ClinicScheduleSlot> newSlots;
        var isNew = existing is null;

        if (existing is null)
        {
            (target, newSlots) = ClinicSchedule.Create(
                tenantId,
                slotsValidation.Value,
                command.Actor.AccountId,
                command.Actor.FullName,
                utcNow);
        }
        else
        {
            target = existing;
            newSlots = target.ReplaceSlots(
                slotsValidation.Value,
                command.Actor.AccountId,
                command.Actor.FullName,
                utcNow);
        }

        await scheduleRepository.SaveClinicScheduleAsync(target, isNew, newSlots, cancellationToken);
        return Result.Success(ScheduleMapper.ToDto(tenantId, target));
    }
}
