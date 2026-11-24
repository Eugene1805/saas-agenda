namespace Saas.Agenda.Infrastructure.Persistence.Tenancy;

public interface ITenantAgendaDbContextFactory
{
    AgendaDbContext CreateDbContext(Guid tenantId);
}
