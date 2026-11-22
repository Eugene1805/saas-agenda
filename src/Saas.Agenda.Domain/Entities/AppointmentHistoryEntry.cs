using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.Domain.Entities;

public sealed class AppointmentHistoryEntry
{
    private AppointmentHistoryEntry()
    {
    }

    private AppointmentHistoryEntry(
        Guid id,
        Guid appointmentId,
        Guid tenantId,
        AppointmentHistoryEventType eventType,
        AppointmentStatus? fromStatus,
        AppointmentStatus toStatus,
        DateTimeOffset? previousStartUtc,
        DateTimeOffset? newStartUtc,
        int? previousDurationMinutes,
        int? newDurationMinutes,
        Guid actorAccountId,
        string actorName,
        string? note,
        DateTimeOffset occurredAtUtc)
    {
        Id = id;
        AppointmentId = appointmentId;
        TenantId = tenantId;
        EventType = eventType;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        PreviousStartUtc = previousStartUtc?.ToUniversalTime();
        NewStartUtc = newStartUtc?.ToUniversalTime();
        PreviousDurationMinutes = previousDurationMinutes;
        NewDurationMinutes = newDurationMinutes;
        ActorAccountId = actorAccountId;
        ActorName = actorName;
        Note = note;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid AppointmentId { get; private set; }
    public Guid TenantId { get; private set; }
    public AppointmentHistoryEventType EventType { get; private set; }
    public AppointmentStatus? FromStatus { get; private set; }
    public AppointmentStatus ToStatus { get; private set; }
    public DateTimeOffset? PreviousStartUtc { get; private set; }
    public DateTimeOffset? NewStartUtc { get; private set; }
    public int? PreviousDurationMinutes { get; private set; }
    public int? NewDurationMinutes { get; private set; }
    public Guid ActorAccountId { get; private set; }
    public string ActorName { get; private set; } = string.Empty;
    public string? Note { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }

    public static AppointmentHistoryEntry ForCreation(
        Guid id,
        Guid appointmentId,
        Guid tenantId,
        DateTimeOffset scheduledStartUtc,
        int durationMinutes,
        Guid actorAccountId,
        string actorName,
        string? note,
        DateTimeOffset occurredAtUtc)
    {
        return new AppointmentHistoryEntry(
            id,
            appointmentId,
            tenantId,
            AppointmentHistoryEventType.CREATED,
            null,
            AppointmentStatus.PROGRAMADA,
            null,
            scheduledStartUtc.ToUniversalTime(),
            null,
            durationMinutes,
            actorAccountId,
            actorName,
            note,
            occurredAtUtc.ToUniversalTime());
    }

    public static AppointmentHistoryEntry ForReschedule(
        Guid id,
        Guid appointmentId,
        Guid tenantId,
        AppointmentStatus currentStatus,
        DateTimeOffset previousStartUtc,
        DateTimeOffset newStartUtc,
        int previousDurationMinutes,
        int newDurationMinutes,
        Guid actorAccountId,
        string actorName,
        string? note,
        DateTimeOffset occurredAtUtc)
    {
        return new AppointmentHistoryEntry(
            id,
            appointmentId,
            tenantId,
            AppointmentHistoryEventType.RESCHEDULED,
            currentStatus,
            currentStatus,
            previousStartUtc.ToUniversalTime(),
            newStartUtc.ToUniversalTime(),
            previousDurationMinutes,
            newDurationMinutes,
            actorAccountId,
            actorName,
            note,
            occurredAtUtc.ToUniversalTime());
    }

    public static AppointmentHistoryEntry ForStatusChange(
        Guid id,
        Guid appointmentId,
        Guid tenantId,
        AppointmentStatus fromStatus,
        AppointmentStatus toStatus,
        Guid actorAccountId,
        string actorName,
        string? note,
        DateTimeOffset occurredAtUtc)
    {
        return new AppointmentHistoryEntry(
            id,
            appointmentId,
            tenantId,
            AppointmentHistoryEventType.STATUS_CHANGED,
            fromStatus,
            toStatus,
            null,
            null,
            null,
            null,
            actorAccountId,
            actorName,
            note,
            occurredAtUtc.ToUniversalTime());
    }
}
