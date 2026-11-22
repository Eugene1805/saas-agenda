using Microsoft.EntityFrameworkCore;
using Npgsql;
using Saas.Agenda.Application.Ports;
using Saas.Agenda.Domain.Entities;
using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.Infrastructure.Persistence.Repositories;

internal sealed class EfAppointmentRepository(AgendaDbContext dbContext) : IAppointmentRepository
{
    public async Task<Appointment?> GetByIdAsync(
        Guid tenantId,
        Guid appointmentId,
        bool includeHistory,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Appointment> query = dbContext.Appointments;
        if (includeHistory)
        {
            query = query
                .AsNoTracking()
                .Include(a => a.History);
        }

        return await query.FirstOrDefaultAsync(
            a => a.TenantId == tenantId && a.Id == appointmentId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Appointment>> ListAsync(
        AppointmentQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        if (filter.RestrictToPatientIds is not null && filter.RestrictToPatientIds.Count == 0)
        {
            return [];
        }

        var query = dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.TenantId == filter.TenantId);

        if (filter.SpecialistId is Guid specialistId)
        {
            query = query.Where(a => a.SpecialistId == specialistId);
        }

        if (filter.PatientId is Guid patientId)
        {
            query = query.Where(a => a.PatientId == patientId);
        }

        if (filter.Status is AppointmentStatus status)
        {
            query = query.Where(a => a.Status == status);
        }

        if (filter.FromUtc is DateTimeOffset fromUtc)
        {
            var normalizedFrom = fromUtc.ToUniversalTime();
            query = query.Where(a => a.ScheduledStartUtc >= normalizedFrom);
        }

        if (filter.ToExclusiveUtc is DateTimeOffset toExclusiveUtc)
        {
            var normalizedTo = toExclusiveUtc.ToUniversalTime();
            query = query.Where(a => a.ScheduledStartUtc < normalizedTo);
        }

        if (filter.RestrictToPatientIds is not null)
        {
            var allowedIds = filter.RestrictToPatientIds.ToList();
            query = query.Where(a => allowedIds.Contains(a.PatientId));
        }

        return await query
            .OrderBy(a => a.ScheduledStartUtc)
            .ThenBy(a => a.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Appointment>> ListActiveBySpecialistInRangeAsync(
        Guid tenantId,
        Guid specialistId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        Guid? excludeAppointmentId,
        CancellationToken cancellationToken = default)
    {
        var normalizedStart = startUtc.ToUniversalTime();
        var normalizedEnd = endUtc.ToUniversalTime();

        var query = dbContext.Appointments
            .AsNoTracking()
            .Where(a =>
                a.TenantId == tenantId &&
                a.SpecialistId == specialistId &&
                a.Status != AppointmentStatus.CANCELADA &&
                a.Status != AppointmentStatus.NO_ASISTIO &&
                a.ScheduledStartUtc < normalizedEnd &&
                a.ScheduledEndUtc > normalizedStart);

        if (excludeAppointmentId is Guid ignoredId)
        {
            query = query.Where(a => a.Id != ignoredId);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Appointment>> ListFutureActiveBySpecialistAsync(
        Guid tenantId,
        Guid specialistId,
        DateTimeOffset fromUtc,
        CancellationToken cancellationToken = default)
    {
        var normalizedFrom = fromUtc.ToUniversalTime();

        return await dbContext.Appointments
            .AsNoTracking()
            .Where(a =>
                a.TenantId == tenantId &&
                a.SpecialistId == specialistId &&
                a.Status != AppointmentStatus.CANCELADA &&
                a.Status != AppointmentStatus.NO_ASISTIO &&
                a.ScheduledStartUtc >= normalizedFrom)
            .OrderBy(a => a.ScheduledStartUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Appointment>> ListFutureActiveByTenantExcludingSpecialistsAsync(
        Guid tenantId,
        IReadOnlySet<Guid> excludedSpecialistIds,
        DateTimeOffset fromUtc,
        CancellationToken cancellationToken = default)
    {
        var normalizedFrom = fromUtc.ToUniversalTime();

        var query = dbContext.Appointments
            .AsNoTracking()
            .Where(a =>
                a.TenantId == tenantId &&
                a.Status != AppointmentStatus.CANCELADA &&
                a.Status != AppointmentStatus.NO_ASISTIO &&
                a.ScheduledStartUtc >= normalizedFrom);

        if (excludedSpecialistIds.Count > 0)
        {
            var excludedList = excludedSpecialistIds.ToList();
            query = query.Where(a => !excludedList.Contains(a.SpecialistId));
        }

        return await query
            .OrderBy(a => a.ScheduledStartUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<AppointmentStatus, int>> CountByStatusAsync(
        Guid? tenantId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toExclusiveUtc,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Appointments.AsNoTracking();

        if (tenantId is Guid tid)
        {
            query = query.Where(a => a.TenantId == tid);
        }

        if (fromUtc is DateTimeOffset f)
        {
            var normalizedFrom = f.ToUniversalTime();
            query = query.Where(a => a.ScheduledStartUtc >= normalizedFrom);
        }

        if (toExclusiveUtc is DateTimeOffset t)
        {
            var normalizedTo = t.ToUniversalTime();
            query = query.Where(a => a.ScheduledStartUtc < normalizedTo);
        }

        var rows = await query
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.Status, r => r.Count);
    }

    public async Task<RepositoryWriteOutcome> AddAsync(
        Appointment appointment,
        AppointmentHistoryEntry initialHistory,
        CancellationToken cancellationToken = default)
    {
        dbContext.Appointments.Add(appointment);
        return await SaveCheckingOverlapAsync(cancellationToken);
    }

    public async Task<RepositoryWriteOutcome> UpdateWithHistoryAsync(
        Appointment appointment,
        AppointmentHistoryEntry historyEntry,
        CancellationToken cancellationToken = default)
    {
        dbContext.AppointmentHistory.Add(historyEntry);
        return await SaveCheckingOverlapAsync(cancellationToken);
    }

    private async Task<RepositoryWriteOutcome> SaveCheckingOverlapAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return RepositoryWriteOutcome.Succeeded;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
                                           pg.SqlState == PostgresErrorCodes.ExclusionViolation)
        {
            dbContext.ChangeTracker.Clear();
            return RepositoryWriteOutcome.ScheduleConflict;
        }
    }
}
