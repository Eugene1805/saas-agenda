using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Saas.Agenda.Infrastructure.Persistence;

public sealed class AgendaDbContextFactory : IDesignTimeDbContextFactory<AgendaDbContext>
{
    public AgendaDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__AgendaTenantDesignTimeDb")
            ?? "Host=localhost;Port=5434;Database=vital_agenda_tenant_designtime";

        var optionsBuilder = new DbContextOptionsBuilder<AgendaDbContext>();
        optionsBuilder.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "agenda"));

        return new AgendaDbContext(optionsBuilder.Options);
    }
}
