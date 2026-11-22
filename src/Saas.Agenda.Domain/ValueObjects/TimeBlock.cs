namespace Saas.Agenda.Domain.ValueObjects;

public sealed record TimeBlock(TimeOnly StartTime, TimeOnly EndTime);
