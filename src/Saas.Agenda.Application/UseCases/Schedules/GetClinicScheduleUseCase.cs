using Saas.Agenda.Application.Ports;
using Saas.Agenda.Domain.Common;
using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.Application.UseCases.Schedules;

public sealed class GetClinicScheduleUseCase(
    IScheduleRepository scheduleRepository,
    ITenantAccessStatePort tenantAccessStatePort)
{
    public async Task<Result<ClinicScheduleDto>> ExecuteAsync(
        AgendaActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var tenantCheck = await AgendaUseCaseGuards.RequireActiveClinicActorAsync(
            actor,
            requireWriteRole: false,
            tenantAccessStatePort,
            cancellationToken);

        if (tenantCheck.IsFailure)
        {
            return Result.Failure<ClinicScheduleDto>(tenantCheck.Error);
        }

        var schedule = await scheduleRepository.GetClinicScheduleAsync(tenantCheck.Value, cancellationToken);
        return Result.Success(ScheduleMapper.ToDto(tenantCheck.Value, schedule));
    }
}
