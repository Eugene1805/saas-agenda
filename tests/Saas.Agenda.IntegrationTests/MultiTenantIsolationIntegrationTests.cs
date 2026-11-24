using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Saas.Agenda.Domain.Common;

namespace Saas.Agenda.IntegrationTests;

public sealed class MultiTenantIsolationIntegrationTests(MultiPostgresFixture fixture)
    : IClassFixture<MultiPostgresFixture>
{
    [Fact]
    public async Task SameSpecialistAndSlotAcrossTwoClinics_UsesSeparatePostgresInstances_AndAggregatesPlatformMetrics()
    {
        await using var factory = fixture.CreateFactory();
        var client = factory.CreateDefaultClient();

        var adminAToken = SignJwt(MultiPostgresFixture.AdminAId, MultiPostgresFixture.ClinicAId, "ADMIN_CLINICA", "Admin Alfa", "admin@alfa.demo");
        var adminBToken = SignJwt(MultiPostgresFixture.AdminBId, MultiPostgresFixture.ClinicBId, "ADMIN_CLINICA", "Admin Beta", "admin@beta.demo");
        var superAdminToken = SignJwt(MultiPostgresFixture.SuperAdminId, null, "SUPER_ADMIN", "Operador SaaS", "super@vital.demo");

        // 1. Configurar horario base de lunes en ambas clínicas por separado.
        await SendAsync(
            client,
            HttpMethod.Put,
            "/v1/schedules/clinic",
            adminAToken,
            new { slots = new[] { new { dayOfWeek = "MONDAY", startTime = "08:00", endTime = "18:00" } } });

        await SendAsync(
            client,
            HttpMethod.Put,
            "/v1/schedules/clinic",
            adminBToken,
            new { slots = new[] { new { dayOfWeek = "MONDAY", startTime = "08:00", endTime = "18:00" } } });

        // 2. Definir horario propio en Alfa sin alterar Beta (Beta conserva respaldo de clínica).
        await SendAsync(
            client,
            HttpMethod.Put,
            $"/v1/schedules/specialists/{MultiPostgresFixture.SharedSpecialistId}",
            adminAToken,
            new
            {
                useClinicFallback = false,
                slots = new[] { new { dayOfWeek = "MONDAY", startTime = "09:00", endTime = "14:00" } },
            });

        using (var specSchedBJson = await SendAndReadJsonAsync(
                   client,
                   HttpMethod.Get,
                   $"/v1/schedules/specialists/{MultiPostgresFixture.SharedSpecialistId}",
                   adminBToken))
        {
            Assert.True(specSchedBJson.RootElement.GetProperty("useClinicFallback").GetBoolean());
        }

        // 3. Próximo lunes futuro en GMT-6.
        var todayGmt6 = Gmt6Clock.TodayGmt6(DateTimeOffset.UtcNow);
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)todayGmt6.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0)
        {
            daysUntilMonday = 7;
        }

        var mondayStr = Gmt6Clock.FormatDateGmt6(todayGmt6.AddDays(daysUntilMonday));
        var startSlot = $"{mondayStr}T10:00:00-06:00";
        var overlapSlot = $"{mondayStr}T10:15:00-06:00";

        // 4. Agendar cita en Clínica Alfa; empalme en Alfa falla con 409, pero mismo horario y especialista en Beta sí pasa (BD separada).
        string aptAId;
        using (var aptAJson = await SendAndReadJsonAsync(
                   client,
                   HttpMethod.Post,
                   "/v1/appointments",
                   adminAToken,
                   new
                   {
                       patientId = MultiPostgresFixture.PatAId,
                       specialistId = MultiPostgresFixture.SharedSpecialistId,
                       scheduledStartGmt6 = startSlot,
                       durationMinutes = 30,
                       reason = "Valoración inicial Alfa",
                   },
                   HttpStatusCode.Created))
        {
            aptAId = aptAJson.RootElement.GetProperty("id").GetString()!;
        }

        using (var overlapReq = new HttpRequestMessage(HttpMethod.Post, "/v1/appointments"))
        {
            overlapReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAToken);
            overlapReq.Content = JsonContent.Create(new
            {
                patientId = MultiPostgresFixture.PatAId,
                specialistId = MultiPostgresFixture.SharedSpecialistId,
                scheduledStartGmt6 = overlapSlot,
                durationMinutes = 30,
                reason = "Empalme dentro de Alfa",
            });
            var overlapResp = await client.SendAsync(overlapReq);
            Assert.Equal(HttpStatusCode.Conflict, overlapResp.StatusCode);
        }

        string aptBId;
        using (var aptBJson = await SendAndReadJsonAsync(
                   client,
                   HttpMethod.Post,
                   "/v1/appointments",
                   adminBToken,
                   new
                   {
                       patientId = MultiPostgresFixture.PatBId,
                       specialistId = MultiPostgresFixture.SharedSpecialistId,
                       scheduledStartGmt6 = startSlot,
                       durationMinutes = 30,
                       reason = "Valoración paralela Beta",
                   },
                   HttpStatusCode.Created))
        {
            aptBId = aptBJson.RootElement.GetProperty("id").GetString()!;
        }

        Assert.NotEqual(aptAId, aptBId);

        // 5. Acceso cruzado por UUID recibe 404 porque viven en PostgreSQL físicos distintos.
        using (var crossReq = new HttpRequestMessage(HttpMethod.Get, $"/v1/appointments/{aptAId}"))
        {
            crossReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminBToken);
            var crossResp = await client.SendAsync(crossReq);
            Assert.Equal(HttpStatusCode.NotFound, crossResp.StatusCode);
        }

        // 6. Métrica clínica consulta solo su BD; métrica plataforma agrega todas las BD configuradas (Alfa + Beta = 2).
        using (var clinicMetricA = await SendAndReadJsonAsync(client, HttpMethod.Get, "/v1/metrics/clinic/activity", adminAToken))
        using (var clinicMetricB = await SendAndReadJsonAsync(client, HttpMethod.Get, "/v1/metrics/clinic/activity", adminBToken))
        using (var platformMetric = await SendAndReadJsonAsync(client, HttpMethod.Get, "/v1/metrics/platform/activity", superAdminToken))
        {
            Assert.Equal(1, clinicMetricA.RootElement.GetProperty("data").GetProperty("total").GetInt32());
            Assert.Equal(1, clinicMetricB.RootElement.GetProperty("data").GetProperty("total").GetInt32());
            Assert.Equal("AVAILABLE", platformMetric.RootElement.GetProperty("status").GetString());
            Assert.Equal(2, platformMetric.RootElement.GetProperty("data").GetProperty("total").GetInt32());
        }

        // 7. Conteos SQL directos por instancia PostgreSQL.
        Assert.Equal(1L, await CountSqlAsync(fixture.TenantADbConnectionString, "SELECT COUNT(*) FROM agenda.appointments;"));
        Assert.Equal(1L, await CountSqlAsync(fixture.TenantADbConnectionString, "SELECT COUNT(*) FROM agenda.specialist_schedules;"));
        Assert.Equal(0L, await CountSqlAsync(fixture.TenantADbConnectionString, $"SELECT COUNT(*) FROM agenda.appointments WHERE tenant_id <> '{MultiPostgresFixture.ClinicAId}';"));

        Assert.Equal(1L, await CountSqlAsync(fixture.TenantBDbConnectionString, "SELECT COUNT(*) FROM agenda.appointments;"));
        Assert.Equal(0L, await CountSqlAsync(fixture.TenantBDbConnectionString, "SELECT COUNT(*) FROM agenda.specialist_schedules;"));
        Assert.Equal(0L, await CountSqlAsync(fixture.TenantBDbConnectionString, $"SELECT COUNT(*) FROM agenda.appointments WHERE tenant_id <> '{MultiPostgresFixture.ClinicBId}';"));
    }

    [Fact]
    public async Task UnconfiguredTenantDatabase_FailsClosedWith503()
    {
        await using var factory = fixture.CreateFactory();
        var client = factory.CreateDefaultClient();

        var unconfiguredToken = SignJwt(
            MultiPostgresFixture.AdminUnconfiguredId,
            MultiPostgresFixture.ClinicUnconfiguredId,
            "ADMIN_CLINICA",
            "Admin Sin BD",
            "admin@sinbd.demo");

        using var req = new HttpRequestMessage(HttpMethod.Get, "/v1/appointments");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", unconfiguredToken);
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, resp.StatusCode);
        using var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal("TENANT_DATABASE_NOT_CONFIGURED", json.RootElement.GetProperty("code").GetString());
    }

    private static string SignJwt(Guid accountId, Guid? tenantId, string role, string fullName, string email)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(MultiPostgresFixture.JwtSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new("sub", accountId.ToString()),
            new("role", role),
            new("full_name", fullName),
            new("email", email),
        };
        if (tenantId is Guid tid)
        {
            claims.Add(new Claim("tenant_id", tid.ToString()));
        }

        var token = new JwtSecurityToken(
            issuer: "saas-identity",
            audience: "vital-saas",
            claims: claims,
            notBefore: now.AddSeconds(-5),
            expires: now.AddMinutes(15),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task SendAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        string bearerToken,
        object? payload = null)
    {
        using var doc = await SendAndReadJsonAsync(client, method, url, bearerToken, payload);
    }

    private static async Task<JsonDocument> SendAndReadJsonAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        string bearerToken,
        object? payload = null,
        HttpStatusCode expectedStatus = HttpStatusCode.OK)
    {
        using var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        if (payload is not null)
        {
            req.Content = JsonContent.Create(payload);
        }

        var resp = await client.SendAsync(req);
        var raw = await resp.Content.ReadAsStringAsync();
        Assert.True(resp.StatusCode == expectedStatus, $"{method} {url} returned {(int)resp.StatusCode} instead of {(int)expectedStatus}: {raw}");
        return JsonDocument.Parse(raw);
    }

    private static async Task<long> CountSqlAsync(string connectionString, string sql)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var scalar = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(scalar);
    }
}
