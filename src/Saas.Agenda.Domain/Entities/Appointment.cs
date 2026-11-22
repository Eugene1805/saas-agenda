using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.Domain.Entities;

public sealed class Appointment
{
    private readonly List<AppointmentHistoryEntry> _history = [];

    private Appointment()
    {
    }

    private Appointment(
        Guid id,
        Guid tenantId,
        Guid patientId,
        Guid specialistId,
        DateTimeOffset scheduledStartUtc,
        DateTimeOffset scheduledEndUtc,
        int durationMinutes,
        string reason,
        AppointmentStatus status,
        Guid createdByAccountId,
        string createdByName,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        PatientId = patientId;
        SpecialistId = specialistId;
        ScheduledStartUtc = scheduledStartUtc.ToUniversalTime();
        ScheduledEndUtc = scheduledEndUtc.ToUniversalTime();
        DurationMinutes = durationMinutes;
        Reason = reason;
        Status = status;
        CreatedByAccountId = createdByAccountId;
        CreatedByName = createdByName;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid SpecialistId { get; private set; }
    public DateTimeOffset ScheduledStartUtc { get; private set; }
    public DateTimeOffset ScheduledEndUtc { get; private set; }
    public int DurationMinutes { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public AppointmentStatus Status { get; private set; }
    public Guid CreatedByAccountId { get; private set; }
    public string CreatedByName { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public IReadOnlyList<AppointmentHistoryEntry> History => _history;

    public bool IsTerminal => Status is AppointmentStatus.COMPLETADA
        or AppointmentStatus.CANCELADA
        or AppointmentStatus.NO_ASISTIO;

    public bool BlocksSchedule => Status is not AppointmentStatus.CANCELADA
        and not AppointmentStatus.NO_ASISTIO;

    public static (Appointment Appointment, AppointmentHistoryEntry InitialHistory) Create(
        Guid id,
        Guid tenantId,
        Guid patientId,
        Guid specialistId,
        DateTimeOffset scheduledStartUtc,
        int durationMinutes,
        string reason,
        Guid createdByAccountId,
        string createdByName,
        DateTimeOffset nowUtc,
        Guid? historyEntryId = null,
        string? creationNote = null)
    {
        var normalizedStartUtc = scheduledStartUtc.ToUniversalTime();
        var normalizedEndUtc = normalizedStartUtc.AddMinutes(durationMinutes);
        var normalizedNowUtc = nowUtc.ToUniversalTime();

        var appointment = new Appointment(
            id,
            tenantId,
            patientId,
            specialistId,
            normalizedStartUtc,
            normalizedEndUtc,
            durationMinutes,
            reason.Trim(),
            AppointmentStatus.PROGRAMADA,
            createdByAccountId,
            createdByName.Trim(),
            normalizedNowUtc,
            normalizedNowUtc);

        var entry = AppointmentHistoryEntry.ForCreation(
            historyEntryId ?? Guid.NewGuid(),
            id,
            tenantId,
            normalizedStartUtc,
            durationMinutes,
            createdByAccountId,
            createdByName.Trim(),
            creationNote,
            normalizedNowUtc);

        appointment._history.Add(entry);
        return (appointment, entry);
    }

    public AppointmentHistoryEntry Reschedule(
        DateTimeOffset newStartUtc,
        int newDurationMinutes,
        Guid actorAccountId,
        string actorName,
        string? note,
        DateTimeOffset nowUtc,
        Guid? historyEntryId = null)
    {
        var normalizedNewStartUtc = newStartUtc.ToUniversalTime();
        var normalizedNewEndUtc = normalizedNewStartUtc.AddMinutes(newDurationMinutes);
        var normalizedNowUtc = nowUtc.ToUniversalTime();

        var previousStart = ScheduledStartUtc;
        var previousDuration = DurationMinutes;

        ScheduledStartUtc = normalizedNewStartUtc;
        ScheduledEndUtc = normalizedNewEndUtc;
        DurationMinutes = newDurationMinutes;
        UpdatedAtUtc = normalizedNowUtc;

        var entry = AppointmentHistoryEntry.ForReschedule(
            historyEntryId ?? Guid.NewGuid(),
            Id,
            TenantId,
            Status,
            previousStart,
            normalizedNewStartUtc,
            previousDuration,
            newDurationMinutes,
            actorAccountId,
            actorName.Trim(),
            string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            normalizedNowUtc);

        _history.Add(entry);
        return entry;
    }

    public AppointmentHistoryEntry TransitionStatus(
        AppointmentStatus nextStatus,
        Guid actorAccountId,
        string actorName,
        string? note,
        DateTimeOffset nowUtc,
        Guid? historyEntryId = null)
    {
        var normalizedNowUtc = nowUtc.ToUniversalTime();
        var previousStatus = Status;

        Status = nextStatus;
        UpdatedAtUtc = normalizedNowUtc;

        var entry = AppointmentHistoryEntry.ForStatusChange(
            historyEntryId ?? Guid.NewGuid(),
            Id,
            TenantId,
            previousStatus,
            nextStatus,
            actorAccountId,
            actorName.Trim(),
            string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            normalizedNowUtc);

        _history.Add(entry);
        return entry;
    }
}
