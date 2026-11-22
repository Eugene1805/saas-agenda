using Saas.Agenda.Application.UseCases.Metrics;
using Saas.Agenda.Domain.Entities;
using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.UnitTests;

public sealed class MetricsUseCasesTests
{
    private readonly InMemoryAppointmentRepository _repo = new();
    private readonly StubTenantAccessStatePort _platform = new();
    private readonly FakeClock _clock = new(DateTimeOffset.Parse("2026-11-21T12:00:00-06:00"));

    [Fact]
    public async Task GetClinicAgendaMetrics_ReturnsAllSixStatusesAndFiltersGmt6Dates()
    {
        var (apt1, _) = Appointment.Create(
            Guid.NewGuid(),
            TestFixtures.NorteTenantId,
            TestFixtures.Pat0101Id,
            TestFixtures.MendozaNorteId,
            DateTimeOffset.Parse("2026-11-20T10:00:00-06:00").ToUniversalTime(),
            30,
            "Consulta 1",
            TestFixtures.AdminNorteId,
            "Admin",
            _clock.UtcNow);
        apt1.TransitionStatus(AppointmentStatus.CONFIRMADA, TestFixtures.AdminNorteId, "Admin", null, _clock.UtcNow);

        var (apt2, _) = Appointment.Create(
            Guid.NewGuid(),
            TestFixtures.NorteTenantId,
            TestFixtures.Pat0102Id,
            TestFixtures.SalazarNorteId,
            DateTimeOffset.Parse("2026-11-21T10:00:00-06:00").ToUniversalTime(),
            30,
            "Consulta 2",
            TestFixtures.AdminNorteId,
            "Admin",
            _clock.UtcNow);

        var (aptValle, _) = Appointment.Create(
            Guid.NewGuid(),
            TestFixtures.ValleTenantId,
            TestFixtures.Pat0201ValleId,
            TestFixtures.MendozaValleId,
            DateTimeOffset.Parse("2026-11-20T11:00:00-06:00").ToUniversalTime(),
            30,
            "Valle",
            TestFixtures.AdminValleId,
            "Admin Valle",
            _clock.UtcNow);

        _repo.Items.AddRange([apt1, apt2, aptValle]);

        var sut = new GetClinicAgendaMetricsUseCase(_repo, _platform, _clock);

        var allNorte = await sut.ExecuteAsync(TestFixtures.AdminNorteActor);
        Assert.True(allNorte.IsSuccess);
        Assert.Equal("AGENDA_ACTIVITY", allNorte.Value.KpiKey);
        Assert.Equal("AVAILABLE", allNorte.Value.Status);
        Assert.Equal(2, allNorte.Value.Data.Total);
        Assert.Equal(6, allNorte.Value.Data.ByStatus.Count);
        Assert.Equal(1, allNorte.Value.Data.ByStatus["PROGRAMADA"]);
        Assert.Equal(1, allNorte.Value.Data.ByStatus["CONFIRMADA"]);
        Assert.Equal(0, allNorte.Value.Data.ByStatus["CANCELADA"]);

        var day20Only = await sut.ExecuteAsync(TestFixtures.AdminNorteActor, "2026-11-20", "2026-11-20");
        Assert.True(day20Only.IsSuccess);
        Assert.Equal(1, day20Only.Value.Data.Total);
        Assert.Equal(1, day20Only.Value.Data.ByStatus["CONFIRMADA"]);

        var forbiddenAux = await sut.ExecuteAsync(TestFixtures.AuxNorteActor);
        Assert.Equal(403, forbiddenAux.Error.HttpStatus);
        Assert.Equal("FORBIDDEN_METRICS_ROLE", forbiddenAux.Error.Code);
    }

    [Fact]
    public async Task GetPlatformAgendaMetrics_AggregatesAcrossTenantsOnlyForSuperAdmin()
    {
        var (aptNorte, _) = Appointment.Create(
            Guid.NewGuid(),
            TestFixtures.NorteTenantId,
            TestFixtures.Pat0101Id,
            TestFixtures.MendozaNorteId,
            DateTimeOffset.Parse("2026-11-20T10:00:00-06:00").ToUniversalTime(),
            30,
            "Norte",
            TestFixtures.AdminNorteId,
            "Admin",
            _clock.UtcNow);

        var (aptValle, _) = Appointment.Create(
            Guid.NewGuid(),
            TestFixtures.ValleTenantId,
            TestFixtures.Pat0201ValleId,
            TestFixtures.MendozaValleId,
            DateTimeOffset.Parse("2026-11-20T11:00:00-06:00").ToUniversalTime(),
            30,
            "Valle",
            TestFixtures.AdminValleId,
            "Admin Valle",
            _clock.UtcNow);

        _repo.Items.AddRange([aptNorte, aptValle]);

        var sut = new GetPlatformAgendaMetricsUseCase(_repo, _clock);

        var result = await sut.ExecuteAsync(TestFixtures.SuperAdminActor);
        Assert.True(result.IsSuccess);
        Assert.Equal("APPOINTMENT_ACTIVITY", result.Value.KpiKey);
        Assert.Equal(2, result.Value.Data.Total);
        Assert.Equal(2, result.Value.Data.ByStatus["PROGRAMADA"]);

        var deniedAdmin = await sut.ExecuteAsync(TestFixtures.AdminNorteActor);
        Assert.Equal(403, deniedAdmin.Error.HttpStatus);
        Assert.Equal("FORBIDDEN_METRICS_ROLE", deniedAdmin.Error.Code);
    }
}
