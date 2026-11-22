using Saas.Agenda.Application.Ports;
using Saas.Agenda.Domain.Common;
using Saas.Agenda.Domain.Policies;

namespace Saas.Agenda.Application.UseCases.Schedules;

public sealed class DeleteSpecialistExceptionUseCase(
    IScheduleRepository scheduleRepository,
    IAppointmentRepository appointmentRepository,
    ITenantAccessStatePort tenantAccessStatePort,
    IAgendaDirectoryPort directoryPort,
    IClockPort clock)
{
    public async Task<Result> ExecuteAsync(
        DeleteSpecialistExceptionCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantCheck = await AgendaUseCaseGuards.RequireScheduleEditorAsync(
            command.Actor,
            command.SpecialistId,
            tenantAccessStatePort,
            cancellationToken);

        if (tenantCheck.IsFailure)
        {
            return Result.Failure(tenantCheck.Error);
        }

        var tenantId = tenantCheck.Value;
        var specialistCheck = await AgendaUseCaseGuards.ValidateSpecialistExistsInTenantAsync(
            tenantId,
            command.SpecialistId,
            directoryPort,
            cancellationToken);

        if (specialistCheck.IsFailure)
        {
            return Result.Failure(specialistCheck.Error);
        }

        var exception = await scheduleRepository.GetSpecialistExceptionByIdAsync(
            tenantId,
            command.SpecialistId,
            command.ExceptionId,
            cancellationToken);

        if (exception is null)
        {
            return Result.Failure(new DomainError(
                "SCHEDULE_EXCEPTION_NOT_FOUND",
                "Excepción de horario no encontrada.",
                404));
        }

        var utcNow = clock.UtcNow;
        var futureApts = await appointmentRepository.ListFutureActiveBySpecialistAsync(
            tenantId,
            command.SpecialistId,
            utcNow,
            cancellationToken);

        var sameDateFutureApts = futureApts
            .Where(a => Gmt6Clock.ToGmt6DateAndTime(a.ScheduledStartUtc).Date == exception.DateGmt6)
            .ToArray();

        if (sameDateFutureApts.Length > 0)
        {
            var specialistSchedule = await scheduleRepository.GetSpecialistScheduleAsync(tenantId, command.SpecialistId, cancellationToken);
            var clinicSchedule = await scheduleRepository.GetClinicScheduleAsync(tenantId, cancellationToken);

            var conflictCheck = SchedulePolicy.ValidateScheduleChangeAgainstAppointments(
                sameDateFutureApts,
                (_, dateGmt6) => SchedulePolicy.ResolveEffectiveDaySchedule(
                    dateGmt6,
                    specialistSchedule,
                    clinicSchedule,
                    exceptionForDate: null),
                utcNow);

            if (conflictCheck.IsFailure)
            {
                return Result.Failure(conflictCheck.Error);
            }
        }

        await scheduleRepository.DeleteSpecialistExceptionAsync(exception, cancellationToken);
        return Result.Success();
    }
}
