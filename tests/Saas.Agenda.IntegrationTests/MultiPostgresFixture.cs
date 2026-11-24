using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Saas.Agenda.IntegrationTests;

public sealed class MultiPostgresFixture : IAsyncLifetime
{
    public static readonly Guid ClinicAId = Guid.Parse("10000000-0000-0000-0000-0000000000a1");
    public static readonly Guid ClinicBId = Guid.Parse("10000000-0000-0000-0000-0000000000b2");
    public static readonly Guid ClinicUnconfiguredId = Guid.Parse("10000000-0000-0000-0000-0000000000c3");

    public static readonly Guid SuperAdminId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid AdminAId = Guid.Parse("20000000-0000-0000-0000-000000000a01");
    public static readonly Guid AdminBId = Guid.Parse("20000000-0000-0000-0000-000000000b01");
    public static readonly Guid AdminUnconfiguredId = Guid.Parse("20000000-0000-0000-0000-000000000c01");

    // Mismo SpecialistId lógico probado en dos clínicas independientes para demostrar aislamiento de GiST y horarios.
    public static readonly Guid SharedSpecialistId = Guid.Parse("20000000-0000-0000-0000-000000000099");

    public static readonly Guid PatAId = Guid.Parse("30000000-0000-0000-0000-000000000a01");
    public static readonly Guid PatBId = Guid.Parse("30000000-0000-0000-0000-000000000b01");
    public static readonly Guid PatCId = Guid.Parse("30000000-0000-0000-0000-000000000c01");

    public const string JwtSigningKey = "AgendaIntegrationJwtSigningKey_AtLeast32Bytes!";

    private readonly List<PostgreSqlContainer> _containers = [];

    public string TenantADbConnectionString { get; private set; } = string.Empty;
    public string TenantBDbConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var extA = Environment.GetEnvironmentVariable("TEST_TENANT_A_DB_CS");
        var extB = Environment.GetEnvironmentVariable("TEST_TENANT_B_DB_CS");

        if (!string.IsNullOrWhiteSpace(extA) && !string.IsNullOrWhiteSpace(extB))
        {
            TenantADbConnectionString = extA;
            TenantBDbConnectionString = extB;
            return;
        }

        var containerA = BuildPostgres("agenda_alfa", "alfa_u", "alfa_p");
        var containerB = BuildPostgres("agenda_beta", "beta_u", "beta_p");

        _containers.AddRange([containerA, containerB]);
        await Task.WhenAll(_containers.Select(c => c.StartAsync()));

        TenantADbConnectionString = containerA.GetConnectionString();
        TenantBDbConnectionString = containerB.GetConnectionString();
    }

    public WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");

            var settings = new Dictionary<string, string?>
            {
                [$"TenantDatabases:Connections:{ClinicAId}"] = TenantADbConnectionString,
                [$"TenantDatabases:Connections:{ClinicBId}"] = TenantBDbConnectionString,
                ["Jwt:Issuer"] = "saas-identity",
                ["Jwt:Audience"] = "vital-saas",
                ["Jwt:SigningKey"] = JwtSigningKey,
                ["Platform:Mode"] = "Local",
                ["Directory:Mode"] = "Local",
                ["AccessControl:Mode"] = "Local",
                ["SeedDemoData"] = "false",
                ["OpenApi:Enabled"] = "false",

                ["Platform:Tenants:0:TenantId"] = ClinicAId.ToString(),
                ["Platform:Tenants:0:Slug"] = "clinica-alfa",
                ["Platform:Tenants:0:Name"] = "Clínica Alfa",
                ["Platform:Tenants:0:State"] = "ACTIVA",

                ["Platform:Tenants:1:TenantId"] = ClinicBId.ToString(),
                ["Platform:Tenants:1:Slug"] = "clinica-beta",
                ["Platform:Tenants:1:Name"] = "Clínica Beta",
                ["Platform:Tenants:1:State"] = "ACTIVA",

                ["Platform:Tenants:2:TenantId"] = ClinicUnconfiguredId.ToString(),
                ["Platform:Tenants:2:Slug"] = "clinica-sin-bd",
                ["Platform:Tenants:2:Name"] = "Clínica Sin BD",
                ["Platform:Tenants:2:State"] = "ACTIVA",

                ["Directory:Specialists:0:SpecialistId"] = SharedSpecialistId.ToString(),
                ["Directory:Specialists:0:TenantId"] = ClinicAId.ToString(),
                ["Directory:Specialists:0:FullName"] = "Dra. Alfa Especialista",
                ["Directory:Specialists:0:Specialty"] = "Cardiología",
                ["Directory:Specialists:0:Active"] = "true",

                ["Directory:Specialists:1:SpecialistId"] = SharedSpecialistId.ToString(),
                ["Directory:Specialists:1:TenantId"] = ClinicBId.ToString(),
                ["Directory:Specialists:1:FullName"] = "Dra. Beta Especialista",
                ["Directory:Specialists:1:Specialty"] = "Cardiología",
                ["Directory:Specialists:1:Active"] = "true",

                ["AccessControl:Patients:0:PatientId"] = PatAId.ToString(),
                ["AccessControl:Patients:0:TenantId"] = ClinicAId.ToString(),
                ["AccessControl:Patients:0:PrimarySpecialistId"] = SharedSpecialistId.ToString(),

                ["AccessControl:Patients:1:PatientId"] = PatBId.ToString(),
                ["AccessControl:Patients:1:TenantId"] = ClinicBId.ToString(),
                ["AccessControl:Patients:1:PrimarySpecialistId"] = SharedSpecialistId.ToString(),

                ["AccessControl:Patients:2:PatientId"] = PatCId.ToString(),
                ["AccessControl:Patients:2:TenantId"] = ClinicUnconfiguredId.ToString(),
            };

            foreach (var kv in settings)
            {
                builder.UseSetting(kv.Key, kv.Value);
            }
        });
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(_containers.Select(c => c.DisposeAsync().AsTask()));
    }

    private static PostgreSqlContainer BuildPostgres(string database, string username, string password) =>
        new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase(database)
            .WithUsername(username)
            .WithPassword(password)
            .Build();
}
