using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Saas.Agenda.Application.Ports;
using Saas.Agenda.Application.UseCases.Appointments;
using Saas.Agenda.Application.UseCases.Metrics;
using Saas.Agenda.Application.UseCases.Schedules;
using Saas.Agenda.Infrastructure.AccessControl;
using Saas.Agenda.Infrastructure.Directory;
using Saas.Agenda.Infrastructure.Persistence;
using Saas.Agenda.Infrastructure.Persistence.Repositories;
using Saas.Agenda.Infrastructure.Persistence.Tenancy;
using Saas.Agenda.Infrastructure.Platform;
using Saas.Agenda.Infrastructure.Security;

namespace Saas.Agenda.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAgendaInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<PlatformOptions>(configuration.GetSection(PlatformOptions.SectionName));
        services.Configure<DirectoryOptions>(configuration.GetSection(DirectoryOptions.SectionName));
        services.Configure<AccessControlOptions>(configuration.GetSection(AccessControlOptions.SectionName));
        services.Configure<TenantDatabasesOptions>(configuration.GetSection(TenantDatabasesOptions.SectionName));

        services.AddSingleton<ITenantConnectionResolver, TenantConnectionResolver>();
        services.AddSingleton<TenantDataSourcePool>();
        services.AddSingleton<ITenantAgendaDbContextFactory, NpgsqlTenantAgendaDbContextFactory>();
        services.AddSingleton<ITenantDatabaseMigratorPort, EfTenantDatabaseMigrator>();
        services.AddScoped<TenantAgendaDbContextAccessor>();

        services.AddSingleton<IClockPort, SystemClockAdapter>();
        services.AddScoped<IAppointmentRepository, EfAppointmentRepository>();
        services.AddScoped<IScheduleRepository, EfScheduleRepository>();
        services.AddScoped<ITenantAccessStatePort, LocalTenantAccessStateAdapter>();
        services.AddScoped<IAgendaDirectoryPort, LocalAgendaDirectoryAdapter>();
        services.AddScoped<IAgendaAccessControlPort, LocalAgendaAccessControlAdapter>();
        services.AddScoped<AgendaDbInitializer>();

        services.AddScoped<ListAppointmentsUseCase>();
        services.AddScoped<GetAppointmentByIdUseCase>();
        services.AddScoped<CreateAppointmentUseCase>();
        services.AddScoped<RescheduleAppointmentUseCase>();
        services.AddScoped<TransitionAppointmentStatusUseCase>();

        services.AddScoped<GetClinicScheduleUseCase>();
        services.AddScoped<UpdateClinicScheduleUseCase>();
        services.AddScoped<GetSpecialistScheduleUseCase>();
        services.AddScoped<UpdateSpecialistScheduleUseCase>();
        services.AddScoped<UpsertSpecialistExceptionUseCase>();
        services.AddScoped<DeleteSpecialistExceptionUseCase>();
        services.AddScoped<GetSpecialistAvailabilityUseCase>();

        services.AddScoped<GetClinicAgendaMetricsUseCase>();
        services.AddScoped<GetPlatformAgendaMetricsUseCase>();

        return services;
    }
}
