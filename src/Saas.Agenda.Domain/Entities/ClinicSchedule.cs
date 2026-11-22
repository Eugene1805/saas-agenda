using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.Domain.Entities;

public sealed class ClinicSchedule
{
    private readonly List<ClinicScheduleSlot> _slots = [];

    private ClinicSchedule()
    {
    }

    private ClinicSchedule(
        Guid tenantId,
        Guid updatedByAccountId,
        string updatedByName,
        DateTimeOffset updatedAtUtc)
    {
        TenantId = tenantId;
        UpdatedByAccountId = updatedByAccountId;
        UpdatedByName = updatedByName;
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
    }

    public Guid TenantId { get; private set; }
    public Guid UpdatedByAccountId { get; private set; }
    public string UpdatedByName { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public IReadOnlyList<ClinicScheduleSlot> Slots => _slots;

    public static (ClinicSchedule Schedule, IReadOnlyList<ClinicScheduleSlot> Slots) Create(
        Guid tenantId,
        IReadOnlyList<(ScheduleDayOfWeek Day, TimeOnly Start, TimeOnly End)> slots,
        Guid updatedByAccountId,
        string updatedByName,
        DateTimeOffset nowUtc)
    {
        var schedule = new ClinicSchedule(
            tenantId,
            updatedByAccountId,
            updatedByName.Trim(),
            nowUtc);

        var createdSlots = schedule.ReplaceSlots(
            slots,
            updatedByAccountId,
            updatedByName,
            nowUtc);

        return (schedule, createdSlots);
    }

    public IReadOnlyList<ClinicScheduleSlot> ReplaceSlots(
        IReadOnlyList<(ScheduleDayOfWeek Day, TimeOnly Start, TimeOnly End)> slots,
        Guid updatedByAccountId,
        string updatedByName,
        DateTimeOffset nowUtc)
    {
        UpdatedByAccountId = updatedByAccountId;
        UpdatedByName = updatedByName.Trim();
        UpdatedAtUtc = nowUtc.ToUniversalTime();

        _slots.Clear();
        var nextSlots = slots
            .OrderBy(s => s.Day)
            .ThenBy(s => s.Start)
            .Select(s => new ClinicScheduleSlot(
                Guid.NewGuid(),
                TenantId,
                s.Day,
                s.Start,
                s.End))
            .ToArray();

        _slots.AddRange(nextSlots);
        return nextSlots;
    }
}
