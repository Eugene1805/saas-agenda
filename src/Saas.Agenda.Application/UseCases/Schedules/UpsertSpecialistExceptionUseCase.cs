using Saas.Agenda.Application.Ports;
using Saas.Agenda.Domain.Common;
using Saas.Agenda.Domain.Entities;
using Saas.Agenda.Domain.Policies;

namespace Saas.Agenda.Application.UseCases.Schedules;

public sealed class UpsertSpecialistExceptionUseCase(
    IScheduleRepository scheduleRepository,
    IAppointmentRepository appointmentRepository,
    ITenantAccessStatePort tenantAccessStatePort,
    IAgendaDirectoryPort directoryPort,
    IClockPort clock)
{
    public async Task<Result<ScheduleExceptionDto>> ExecuteAsync(
        UpsertSpecialistExceptionCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantCheck = await AgendaUseCaseGuards.RequireScheduleEditorAsync(
            command.Actor,
            command.SpecialistId,
            tenantAccessStatePort,
            cancellationToken);

        if (tenantCheck.IsFailure)
        {
            return Result.Failure<ScheduleExceptionDto>(tenantCheck.Error);
        }

        var tenantId = tenantCheck.Value;
        var specialistCheck = await AgendaUseCaseGuards.ValidateActiveSpecialistAsync(
            tenantId,
            command.SpecialistId,
            directoryPort,
            cancellationToken);

        if (specialistCheck.IsFailure)
        {
            return Result.Failure<ScheduleExceptionDto>(specialistCheck.Error);
        }

        var utcNow = clock.UtcNow;
        var validation = SchedulePolicy.ValidateExceptionInput(
            command.DateGmt6,
            command.IsUnavailable,
            command.CustomStartTime,
            command.CustomEndTime,
            command.Reason,
            Gmt6Clock.TodayGmt6(utcNow));

        if (validation.IsFailure)
        {
            return Result.Failure<ScheduleExceptionDto>(validation.Error);
        }

        var v = validation.Value;
        var futureApts = await appointmentRepository.ListFutureActiveBySpecialistAsync(
            tenantId,
            command.SpecialistId,
            utcNow,
            cancellationToken);

        var sameDateFutureApts = futureApts
            .Where(a => Gmt6Clock.ToGmt6DateAndTime(a.ScheduledStartUtc).Date == v.DateGmt6)
            .ToArray();

        if (sameDateFutureApts.Length > 0)
        {
            var proposedCandidate = SpecialistScheduleException.Create(
                Guid.NewGuid(),
                tenantId,
                command.SpecialistId,
                v.DateGmt6,
                v.IsUnavailable,
                v.CustomStartTime,
                v.CustomEndTime,
                v.NormalizedReason,
                command.Actor.AccountId,
                command.Actor.FullName,
                utcNow);

            var conflictCheck = SchedulePolicy.ValidateScheduleChangeAgainstAppointments(
                sameDateFutureApts,
                (_, dateGmt6) => SchedulePolicy.ResolveEffectiveDaySchedule(
                    dateGmt6,
                    specialistSchedule: null,
                    clinicSchedule: null,
                    proposedCandidate),
                utcNow);

            if (conflictCheck.IsFailure)
            {
                return Result.Failure<ScheduleExceptionDto>(conflictCheck.Error);
            }
        }

        var existing = await scheduleRepository.GetSpecialistExceptionByDateAsync(
            tenantId,
            command.SpecialistId,
            v.DateGmt6,
            cancellationToken);

        SpecialistScheduleException target;
        var isNew = existing is null;

        if (existing is null)
        {
            target = SpecialistScheduleException.Create(
                Guid.NewGuid(),
                tenantId,
                command.SpecialistId,
                v.DateGmt6,
                v.IsUnavailable,
                v.CustomStartTime,
                v.CustomEndTime,
                v.NormalizedReason,
                command.Actor.AccountId,
                command.Actor.FullName,
                utcNow);
        }
        else
        {
            target = existing;
            target.Update(
                v.IsUnavailable,
                v.CustomStartTime,
                v.CustomEndTime,
                v.NormalizedReason,
                command.Actor.AccountId,
                command.Actor.FullName,
                utcNow);
        }

        await scheduleRepository.UpsertSpecialistExceptionAsync(target, isNew, cancellationToken);
        return Result.Success(ScheduleMapper.ToDto(target));
    }
}
