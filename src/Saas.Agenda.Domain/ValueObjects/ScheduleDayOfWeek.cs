namespace Saas.Agenda.Domain.ValueObjects;

public enum ScheduleDayOfWeek
{
    MONDAY = 1,
    TUESDAY = 2,
    WEDNESDAY = 3,
    THURSDAY = 4,
    FRIDAY = 5,
    SATURDAY = 6,
    SUNDAY = 7,
}

public static class ScheduleDayOfWeekExtensions
{
    public static ScheduleDayOfWeek FromDayOfWeek(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Monday => ScheduleDayOfWeek.MONDAY,
        DayOfWeek.Tuesday => ScheduleDayOfWeek.TUESDAY,
        DayOfWeek.Wednesday => ScheduleDayOfWeek.WEDNESDAY,
        DayOfWeek.Thursday => ScheduleDayOfWeek.THURSDAY,
        DayOfWeek.Friday => ScheduleDayOfWeek.FRIDAY,
        DayOfWeek.Saturday => ScheduleDayOfWeek.SATURDAY,
        DayOfWeek.Sunday => ScheduleDayOfWeek.SUNDAY,
        _ => throw new ArgumentOutOfRangeException(nameof(dayOfWeek), dayOfWeek, null),
    };

    public static ScheduleDayOfWeek FromDateOnly(DateOnly date) =>
        FromDayOfWeek(date.DayOfWeek);
}
