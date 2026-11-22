using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.Domain.Entities;

public sealed class SpecialistSchedule
{
    private readonly List<SpecialistScheduleSlot> _slots = [];

    private SpecialistSchedule()
    {
    }

    private SpecialistSchedule(
        Guid id,
        Guid tenantId,
        Guid specialistId,
        bool useClinicFallback,
        Guid updatedByAccountId,
        string updatedByName,
        DateTimeOffset updatedAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        SpecialistId = specialistId;
        UseClinicFallback = useClinicFallback;
        UpdatedByAccountId = updatedByAccountId;
        UpdatedByName = updatedByName;
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SpecialistId { get; private set; }
    public bool UseClinicFallback { get; private set; }
    public Guid UpdatedByAccountId { get; private set; }
    public string UpdatedByName { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public IReadOnlyList<SpecialistScheduleSlot> Slots => _slots;

    public static (SpecialistSchedule Schedule, IReadOnlyList<SpecialistScheduleSlot> Slots) Create(
        Guid id,
        Guid tenantId,
        Guid specialistId,
        bool useClinicFallback,
        IReadOnlyList<(ScheduleDayOfWeek Day, TimeOnly Start, TimeOnly End)> slots,
        Guid updatedByAccountId,
        string updatedByName,
        DateTimeOffset nowUtc)
    {
        var schedule = new SpecialistSchedule(
            id,
            tenantId,
            specialistId,
            useClinicFallback,
            updatedByAccountId,
            updatedByName.Trim(),
            nowUtc);

        var createdSlots = schedule.Update(
            useClinicFallback,
            slots,
            updatedByAccountId,
            updatedByName,
            nowUtc);

        return (schedule, createdSlots);
    }

    public IReadOnlyList<SpecialistScheduleSlot> Update(
        bool useClinicFallback,
        IReadOnlyList<(ScheduleDayOfWeek Day, TimeOnly Start, TimeOnly End)> slots,
        Guid updatedByAccountId,
        string updatedByName,
        DateTimeOffset nowUtc)
    {
        UseClinicFallback = useClinicFallback;
        UpdatedByAccountId = updatedByAccountId;
        UpdatedByName = updatedByName.Trim();
        UpdatedAtUtc = nowUtc.ToUniversalTime();

        _slots.Clear();
        if (useClinicFallback)
        {
            return [];
        }

        var nextSlots = slots
            .OrderBy(s => s.Day)
            .ThenBy(s => s.Start)
            .Select(s => new SpecialistScheduleSlot(
                Guid.NewGuid(),
                Id,
                s.Day,
                s.Start,
                s.End))
            .ToArray();

        _slots.AddRange(nextSlots);
        return nextSlots;
    }
}
