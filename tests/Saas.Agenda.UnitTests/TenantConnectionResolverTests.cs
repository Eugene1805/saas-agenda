using Microsoft.Extensions.Options;
using Saas.Agenda.Infrastructure.Persistence.Tenancy;

namespace Saas.Agenda.UnitTests;

public sealed class TenantConnectionResolverTests
{
    private sealed class StaticOptionsMonitor(TenantDatabasesOptions currentValue)
        : IOptionsMonitor<TenantDatabasesOptions>
    {
        public TenantDatabasesOptions CurrentValue => currentValue;
        public TenantDatabasesOptions Get(string? name) => currentValue;
        public IDisposable? OnChange(Action<TenantDatabasesOptions, string?> listener) => null;
    }

    [Fact]
    public void TryResolveConnectionString_SupportsConnectionsMap_AndCnpgSecretsDirectoryFormats()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vital-ag-secrets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var uriTenantId = Guid.Parse("10000000-0000-0000-0000-000000000031");
            var cnpgFilesTenantId = Guid.Parse("10000000-0000-0000-0000-000000000032");
            var mapTenantId = Guid.Parse("10000000-0000-0000-0000-000000000033");

            var uriDir = Path.Combine(tempRoot, uriTenantId.ToString("D"));
            Directory.CreateDirectory(uriDir);
            File.WriteAllText(
                Path.Combine(uriDir, "uri"),
                "postgresql://ag_u:Sec%21ret1@pg-ag-31.internal:5432/agenda_t31?sslmode=Require\n");

            var filesDir = Path.Combine(tempRoot, cnpgFilesTenantId.ToString("D"));
            Directory.CreateDirectory(filesDir);
            File.WriteAllText(Path.Combine(filesDir, "host"), "pg-ag-32.internal\n");
            File.WriteAllText(Path.Combine(filesDir, "port"), "5434");
            File.WriteAllText(Path.Combine(filesDir, "dbname"), "agenda_t32");
            File.WriteAllText(Path.Combine(filesDir, "username"), "ag_32");
            File.WriteAllText(Path.Combine(filesDir, "password"), "pass_32");
            File.WriteAllText(Path.Combine(filesDir, "sslmode"), "Disable");

            var resolver = new TenantConnectionResolver(new StaticOptionsMonitor(new TenantDatabasesOptions
            {
                SecretsDirectory = tempRoot,
                Connections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [mapTenantId.ToString("D")] = "Host=pg-ag-33;Port=5432;Database=agenda_t33;Username=u33;Password=p33",
                },
            }));

            Assert.True(resolver.TryResolveConnectionString(uriTenantId, out var cs31));
            Assert.Contains("Host=pg-ag-31.internal", cs31);
            Assert.Contains("Database=agenda_t31", cs31);
            Assert.Contains("SSL Mode=Require", cs31, StringComparison.OrdinalIgnoreCase);

            Assert.True(resolver.TryResolveConnectionString(cnpgFilesTenantId, out var cs32));
            Assert.Contains("Host=pg-ag-32.internal", cs32);
            Assert.Contains("Port=5434", cs32);
            Assert.Contains("Database=agenda_t32", cs32);

            Assert.True(resolver.TryResolveConnectionString(mapTenantId, out var cs33));
            Assert.Contains("Database=agenda_t33", cs33);

            Assert.False(resolver.TryResolveConnectionString(Guid.Empty, out _));
            Assert.False(resolver.TryResolveConnectionString(Guid.NewGuid(), out _));

            var configured = resolver.ListConfiguredTenantIds();
            Assert.Contains(uriTenantId, configured);
            Assert.Contains(cnpgFilesTenantId, configured);
            Assert.Contains(mapTenantId, configured);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}
