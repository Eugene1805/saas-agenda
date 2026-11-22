using Saas.Agenda.Domain.Entities;

namespace Saas.Agenda.Application.Ports;

public interface IScheduleRepository
{
    Task<ClinicSchedule?> GetClinicScheduleAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task SaveClinicScheduleAsync(
        ClinicSchedule schedule,
        bool isNew,
        IReadOnlyList<ClinicScheduleSlot> newSlots,
        CancellationToken cancellationToken = default);

    Task<SpecialistSchedule?> GetSpecialistScheduleAsync(
        Guid tenantId,
        Guid specialistId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlySet<Guid>> ListCustomSpecialistIdsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task SaveSpecialistScheduleAsync(
        SpecialistSchedule schedule,
        bool isNew,
        IReadOnlyList<SpecialistScheduleSlot> newSlots,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SpecialistScheduleException>> ListSpecialistExceptionsAsync(
        Guid tenantId,
        Guid specialistId,
        DateOnly? fromDateGmt6 = null,
        CancellationToken cancellationToken = default);

    Task<SpecialistScheduleException?> GetSpecialistExceptionByDateAsync(
        Guid tenantId,
        Guid specialistId,
        DateOnly dateGmt6,
        CancellationToken cancellationToken = default);

    Task<SpecialistScheduleException?> GetSpecialistExceptionByIdAsync(
        Guid tenantId,
        Guid specialistId,
        Guid exceptionId,
        CancellationToken cancellationToken = default);

    Task UpsertSpecialistExceptionAsync(
        SpecialistScheduleException exception,
        bool isNew,
        CancellationToken cancellationToken = default);

    Task DeleteSpecialistExceptionAsync(
        SpecialistScheduleException exception,
        CancellationToken cancellationToken = default);
}
