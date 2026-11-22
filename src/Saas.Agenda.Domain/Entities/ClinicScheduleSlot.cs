using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.Domain.Entities;

public sealed class ClinicScheduleSlot
{
    private ClinicScheduleSlot()
    {
    }

    internal ClinicScheduleSlot(
        Guid id,
        Guid tenantId,
        ScheduleDayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime)
    {
        Id = id;
        TenantId = tenantId;
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public ScheduleDayOfWeek DayOfWeek { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
}
