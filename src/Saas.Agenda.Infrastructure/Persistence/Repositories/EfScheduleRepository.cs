using Microsoft.EntityFrameworkCore;
using Saas.Agenda.Application.Ports;
using Saas.Agenda.Domain.Entities;
using Saas.Agenda.Infrastructure.Persistence.Tenancy;

namespace Saas.Agenda.Infrastructure.Persistence.Repositories;

internal sealed class EfScheduleRepository(TenantAgendaDbContextAccessor contextAccessor)
    : IScheduleRepository
{
    public Task<ClinicSchedule?> GetClinicScheduleAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync(
            contextAccessor,
            tenantId,
            dbContext => dbContext.ClinicSchedules
                .AsNoTracking()
                .Include(s => s.Slots)
                .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken),
            cancellationToken);
    }

    public Task SaveClinicScheduleAsync(
        ClinicSchedule schedule,
        bool isNew,
        IReadOnlyList<ClinicScheduleSlot> newSlots,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync(
            contextAccessor,
            schedule.TenantId,
            async dbContext =>
            {
                if (isNew)
                {
                    dbContext.ClinicSchedules.Add(schedule);
                }
                else
                {
                    var tracked = await dbContext.ClinicSchedules
                        .Include(s => s.Slots)
                        .FirstAsync(s => s.TenantId == schedule.TenantId, cancellationToken);

                    if (tracked.Slots.Count > 0)
                    {
                        dbContext.ClinicScheduleSlots.RemoveRange(tracked.Slots);
                    }

                    dbContext.Entry(tracked).CurrentValues.SetValues(schedule);
                    if (newSlots.Count > 0)
                    {
                        dbContext.ClinicScheduleSlots.AddRange(newSlots);
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
            },
            cancellationToken);
    }

    public Task<SpecialistSchedule?> GetSpecialistScheduleAsync(
        Guid tenantId,
        Guid specialistId,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync(
            contextAccessor,
            tenantId,
            dbContext => dbContext.SpecialistSchedules
                .AsNoTracking()
                .Include(s => s.Slots)
                .FirstOrDefaultAsync(
                    s => s.TenantId == tenantId && s.SpecialistId == specialistId,
                    cancellationToken),
            cancellationToken);
    }

    public Task<IReadOnlySet<Guid>> ListCustomSpecialistIdsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync<IReadOnlySet<Guid>>(
            contextAccessor,
            tenantId,
            async dbContext =>
            {
                var ids = await dbContext.SpecialistSchedules
                    .AsNoTracking()
                    .Where(s => s.TenantId == tenantId && !s.UseClinicFallback)
                    .Select(s => s.SpecialistId)
                    .ToListAsync(cancellationToken);

                return ids.ToHashSet();
            },
            cancellationToken);
    }

    public Task SaveSpecialistScheduleAsync(
        SpecialistSchedule schedule,
        bool isNew,
        IReadOnlyList<SpecialistScheduleSlot> newSlots,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync(
            contextAccessor,
            schedule.TenantId,
            async dbContext =>
            {
                if (isNew)
                {
                    dbContext.SpecialistSchedules.Add(schedule);
                }
                else
                {
                    var tracked = await dbContext.SpecialistSchedules
                        .Include(s => s.Slots)
                        .FirstAsync(s => s.Id == schedule.Id, cancellationToken);

                    if (tracked.Slots.Count > 0)
                    {
                        dbContext.SpecialistScheduleSlots.RemoveRange(tracked.Slots);
                    }

                    dbContext.Entry(tracked).CurrentValues.SetValues(schedule);
                    if (newSlots.Count > 0)
                    {
                        dbContext.SpecialistScheduleSlots.AddRange(newSlots);
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<SpecialistScheduleException>> ListSpecialistExceptionsAsync(
        Guid tenantId,
        Guid specialistId,
        DateOnly? fromDateGmt6 = null,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync<IReadOnlyList<SpecialistScheduleException>>(
            contextAccessor,
            tenantId,
            async dbContext =>
            {
                var query = dbContext.SpecialistScheduleExceptions
                    .AsNoTracking()
                    .Where(e => e.TenantId == tenantId && e.SpecialistId == specialistId);

                if (fromDateGmt6 is DateOnly fromDate)
                {
                    query = query.Where(e => e.DateGmt6 >= fromDate);
                }

                return await query
                    .OrderBy(e => e.DateGmt6)
                    .ToListAsync(cancellationToken);
            },
            cancellationToken);
    }

    public Task<SpecialistScheduleException?> GetSpecialistExceptionByDateAsync(
        Guid tenantId,
        Guid specialistId,
        DateOnly dateGmt6,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync(
            contextAccessor,
            tenantId,
            dbContext => dbContext.SpecialistScheduleExceptions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    e => e.TenantId == tenantId && e.SpecialistId == specialistId && e.DateGmt6 == dateGmt6,
                    cancellationToken),
            cancellationToken);
    }

    public Task<SpecialistScheduleException?> GetSpecialistExceptionByIdAsync(
        Guid tenantId,
        Guid specialistId,
        Guid exceptionId,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync(
            contextAccessor,
            tenantId,
            dbContext => dbContext.SpecialistScheduleExceptions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    e => e.TenantId == tenantId && e.SpecialistId == specialistId && e.Id == exceptionId,
                    cancellationToken),
            cancellationToken);
    }

    public Task UpsertSpecialistExceptionAsync(
        SpecialistScheduleException exception,
        bool isNew,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync(
            contextAccessor,
            exception.TenantId,
            async dbContext =>
            {
                if (isNew)
                {
                    dbContext.SpecialistScheduleExceptions.Add(exception);
                }
                else
                {
                    dbContext.SpecialistScheduleExceptions.Update(exception);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
            },
            cancellationToken);
    }

    public Task DeleteSpecialistExceptionAsync(
        SpecialistScheduleException exception,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync(
            contextAccessor,
            exception.TenantId,
            async dbContext =>
            {
                dbContext.SpecialistScheduleExceptions.Remove(exception);
                await dbContext.SaveChangesAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
            },
            cancellationToken);
    }
}
