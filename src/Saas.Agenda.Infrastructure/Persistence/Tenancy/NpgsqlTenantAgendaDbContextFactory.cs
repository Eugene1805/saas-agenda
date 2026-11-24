using Microsoft.EntityFrameworkCore;

namespace Saas.Agenda.Infrastructure.Persistence.Tenancy;

internal sealed class NpgsqlTenantAgendaDbContextFactory(TenantDataSourcePool dataSourcePool)
    : ITenantAgendaDbContextFactory
{
    public AgendaDbContext CreateDbContext(Guid tenantId)
    {
        var (_, dataSource) = dataSourcePool.GetConnection(tenantId);

        var optionsBuilder = new DbContextOptionsBuilder<AgendaDbContext>();
        optionsBuilder.UseNpgsql(
            dataSource,
            npgsql => npgsql
                .MigrationsHistoryTable("__EFMigrationsHistory", "agenda")
                .EnableRetryOnFailure());

        return new AgendaDbContext(optionsBuilder.Options);
    }
}
