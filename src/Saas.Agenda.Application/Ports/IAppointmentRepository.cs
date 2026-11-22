using Saas.Agenda.Domain.Entities;
using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.Application.Ports;

public enum RepositoryWriteOutcome
{
    Succeeded,
    ScheduleConflict,
}

public sealed record AppointmentQueryFilter(
    Guid TenantId,
    Guid? SpecialistId = null,
    Guid? PatientId = null,
    AppointmentStatus? Status = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToExclusiveUtc = null,
    IReadOnlySet<Guid>? RestrictToPatientIds = null);

public interface IAppointmentRepository
{
    Task<Appointment?> GetByIdAsync(
        Guid tenantId,
        Guid appointmentId,
        bool includeHistory,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Appointment>> ListAsync(
        AppointmentQueryFilter filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Appointment>> ListActiveBySpecialistInRangeAsync(
        Guid tenantId,
        Guid specialistId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        Guid? excludeAppointmentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Appointment>> ListFutureActiveBySpecialistAsync(
        Guid tenantId,
        Guid specialistId,
        DateTimeOffset fromUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Appointment>> ListFutureActiveByTenantExcludingSpecialistsAsync(
        Guid tenantId,
        IReadOnlySet<Guid> excludedSpecialistIds,
        DateTimeOffset fromUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<AppointmentStatus, int>> CountByStatusAsync(
        Guid? tenantId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toExclusiveUtc,
        CancellationToken cancellationToken = default);

    Task<RepositoryWriteOutcome> AddAsync(
        Appointment appointment,
        AppointmentHistoryEntry initialHistory,
        CancellationToken cancellationToken = default);

    Task<RepositoryWriteOutcome> UpdateWithHistoryAsync(
        Appointment appointment,
        AppointmentHistoryEntry historyEntry,
        CancellationToken cancellationToken = default);
}
