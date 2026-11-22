namespace Saas.Agenda.Domain.Entities;

public sealed class SpecialistScheduleException
{
    private SpecialistScheduleException()
    {
    }

    private SpecialistScheduleException(
        Guid id,
        Guid tenantId,
        Guid specialistId,
        DateOnly dateGmt6,
        bool isUnavailable,
        TimeOnly? customStartTime,
        TimeOnly? customEndTime,
        string reason,
        Guid createdByAccountId,
        string createdByName,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        SpecialistId = specialistId;
        DateGmt6 = dateGmt6;
        IsUnavailable = isUnavailable;
        CustomStartTime = customStartTime;
        CustomEndTime = customEndTime;
        Reason = reason;
        CreatedByAccountId = createdByAccountId;
        CreatedByName = createdByName;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SpecialistId { get; private set; }
    public DateOnly DateGmt6 { get; private set; }
    public bool IsUnavailable { get; private set; }
    public TimeOnly? CustomStartTime { get; private set; }
    public TimeOnly? CustomEndTime { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid CreatedByAccountId { get; private set; }
    public string CreatedByName { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static SpecialistScheduleException Create(
        Guid id,
        Guid tenantId,
        Guid specialistId,
        DateOnly dateGmt6,
        bool isUnavailable,
        TimeOnly? customStartTime,
        TimeOnly? customEndTime,
        string reason,
        Guid createdByAccountId,
        string createdByName,
        DateTimeOffset nowUtc)
    {
        var normalizedNow = nowUtc.ToUniversalTime();
        return new SpecialistScheduleException(
            id,
            tenantId,
            specialistId,
            dateGmt6,
            isUnavailable,
            isUnavailable ? null : customStartTime,
            isUnavailable ? null : customEndTime,
            reason.Trim(),
            createdByAccountId,
            createdByName.Trim(),
            normalizedNow,
            normalizedNow);
    }

    public void Update(
        bool isUnavailable,
        TimeOnly? customStartTime,
        TimeOnly? customEndTime,
        string reason,
        Guid actorAccountId,
        string actorName,
        DateTimeOffset nowUtc)
    {
        IsUnavailable = isUnavailable;
        CustomStartTime = isUnavailable ? null : customStartTime;
        CustomEndTime = isUnavailable ? null : customEndTime;
        Reason = reason.Trim();
        CreatedByAccountId = actorAccountId;
        CreatedByName = actorName.Trim();
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }
}
