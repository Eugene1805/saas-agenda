using System.Net.Sockets;
using Npgsql;
using Saas.Agenda.Application.Ports;
using Saas.Agenda.Infrastructure.Persistence.Tenancy;

namespace Saas.Agenda.Infrastructure.Persistence.Repositories;

internal static class TenantPersistenceHelper
{
    public static async Task<T> ExecuteAsync<T>(
        TenantAgendaDbContextAccessor accessor,
        Guid tenantId,
        Func<AgendaDbContext, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dbContext = await accessor.GetDbContextAsync(tenantId, cancellationToken);
            return await operation(dbContext);
        }
        catch (Exception ex) when (ex is not OperationCanceledException &&
                                   ex is not TenantDatabaseUnavailableException &&
                                   IsConnectivityFailure(ex))
        {
            throw new TenantDatabaseUnavailableException(
                tenantId,
                "TENANT_DATABASE_UNAVAILABLE",
                $"Error de conectividad al consultar la base de datos del tenant {tenantId}.",
                ex);
        }
    }

    public static async Task ExecuteAsync(
        TenantAgendaDbContextAccessor accessor,
        Guid tenantId,
        Func<AgendaDbContext, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(
            accessor,
            tenantId,
            async dbContext =>
            {
                await operation(dbContext);
                return true;
            },
            cancellationToken);
    }

    public static async Task<T> ExecuteIsolatedAsync<T>(
        ITenantAgendaDbContextFactory factory,
        ITenantDatabaseMigratorPort migrator,
        Guid tenantId,
        Func<AgendaDbContext, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await migrator.EnsureMigratedAsync(tenantId, cancellationToken);
            await using var dbContext = factory.CreateDbContext(tenantId);
            return await operation(dbContext);
        }
        catch (Exception ex) when (ex is not OperationCanceledException &&
                                   ex is not TenantDatabaseUnavailableException &&
                                   IsConnectivityFailure(ex))
        {
            throw new TenantDatabaseUnavailableException(
                tenantId,
                "TENANT_DATABASE_UNAVAILABLE",
                $"Error de conectividad al consultar la base de datos del tenant {tenantId}.",
                ex);
        }
    }

    public static bool IsConnectivityFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is TimeoutException or SocketException)
            {
                return true;
            }

            if (current is PostgresException postgresException)
            {
                if (postgresException.IsTransient ||
                    postgresException.SqlState.StartsWith("08", StringComparison.Ordinal) ||
                    postgresException.SqlState.StartsWith("57", StringComparison.Ordinal) ||
                    postgresException.SqlState.StartsWith("53", StringComparison.Ordinal))
                {
                    return true;
                }

                return false;
            }

            if (current is NpgsqlException)
            {
                return true;
            }
        }

        return false;
    }
}
