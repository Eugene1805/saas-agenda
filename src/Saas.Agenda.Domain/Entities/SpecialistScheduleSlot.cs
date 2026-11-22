using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.Domain.Entities;

public sealed class SpecialistScheduleSlot
{
    private SpecialistScheduleSlot()
    {
    }

    internal SpecialistScheduleSlot(
        Guid id,
        Guid specialistScheduleId,
        ScheduleDayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime)
    {
        Id = id;
        SpecialistScheduleId = specialistScheduleId;
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
    }

    public Guid Id { get; private set; }
    public Guid SpecialistScheduleId { get; private set; }
    public ScheduleDayOfWeek DayOfWeek { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
}
