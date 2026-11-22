using Saas.Agenda.Application.Ports;
using Saas.Agenda.Domain.Common;
using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.Application.UseCases.Schedules;

public sealed class GetSpecialistScheduleUseCase(
    IScheduleRepository scheduleRepository,
    ITenantAccessStatePort tenantAccessStatePort,
    IAgendaDirectoryPort directoryPort,
    IClockPort clock)
{
    public async Task<Result<SpecialistScheduleDto>> ExecuteAsync(
        AgendaActorContext actor,
        Guid specialistId,
        CancellationToken cancellationToken = default)
    {
        var tenantCheck = await AgendaUseCaseGuards.RequireActiveClinicActorAsync(
            actor,
            requireWriteRole: false,
            tenantAccessStatePort,
            cancellationToken);

        if (tenantCheck.IsFailure)
        {
            return Result.Failure<SpecialistScheduleDto>(tenantCheck.Error);
        }

        if (actor.Role == OrgRole.ESPECIALISTA && actor.AccountId != specialistId)
        {
            return Result.Failure<SpecialistScheduleDto>(new DomainError(
                "FORBIDDEN_SCHEDULE_SCOPE",
                "Como especialista solo puedes consultar tu propio horario laboral.",
                403));
        }

        var tenantId = tenantCheck.Value;
        var specialistCheck = await AgendaUseCaseGuards.ValidateSpecialistExistsInTenantAsync(
            tenantId,
            specialistId,
            directoryPort,
            cancellationToken);

        if (specialistCheck.IsFailure)
        {
            return Result.Failure<SpecialistScheduleDto>(specialistCheck.Error);
        }

        var specialistSchedule = await scheduleRepository.GetSpecialistScheduleAsync(tenantId, specialistId, cancellationToken);
        var clinicSchedule = await scheduleRepository.GetClinicScheduleAsync(tenantId, cancellationToken);
        var upcomingExceptions = await scheduleRepository.ListSpecialistExceptionsAsync(
            tenantId,
            specialistId,
            Gmt6Clock.TodayGmt6(clock.UtcNow),
            cancellationToken);

        return Result.Success(ScheduleMapper.ToDto(
            tenantId,
            specialistId,
            specialistSchedule,
            clinicSchedule,
            upcomingExceptions));
    }
}
