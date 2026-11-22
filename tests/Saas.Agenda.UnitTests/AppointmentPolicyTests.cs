using Saas.Agenda.Domain.Entities;
using Saas.Agenda.Domain.Policies;
using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.UnitTests;

public sealed class AppointmentPolicyTests
{
    [Theory]
    [InlineData(AppointmentStatus.PROGRAMADA, AppointmentStatus.CONFIRMADA)]
    [InlineData(AppointmentStatus.PROGRAMADA, AppointmentStatus.EN_CURSO)]
    [InlineData(AppointmentStatus.PROGRAMADA, AppointmentStatus.CANCELADA)]
    [InlineData(AppointmentStatus.PROGRAMADA, AppointmentStatus.NO_ASISTIO)]
    [InlineData(AppointmentStatus.CONFIRMADA, AppointmentStatus.EN_CURSO)]
    [InlineData(AppointmentStatus.CONFIRMADA, AppointmentStatus.CANCELADA)]
    [InlineData(AppointmentStatus.CONFIRMADA, AppointmentStatus.NO_ASISTIO)]
    [InlineData(AppointmentStatus.EN_CURSO, AppointmentStatus.COMPLETADA)]
    [InlineData(AppointmentStatus.EN_CURSO, AppointmentStatus.CANCELADA)]
    public void ValidateTransition_AllowsValidTransitions(AppointmentStatus from, AppointmentStatus to)
    {
        var result = AppointmentPolicy.ValidateTransition(from, to);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(AppointmentStatus.PROGRAMADA, AppointmentStatus.COMPLETADA)]
    [InlineData(AppointmentStatus.CONFIRMADA, AppointmentStatus.COMPLETADA)]
    [InlineData(AppointmentStatus.EN_CURSO, AppointmentStatus.CONFIRMADA)]
    [InlineData(AppointmentStatus.EN_CURSO, AppointmentStatus.NO_ASISTIO)]
    [InlineData(AppointmentStatus.COMPLETADA, AppointmentStatus.PROGRAMADA)]
    [InlineData(AppointmentStatus.COMPLETADA, AppointmentStatus.CANCELADA)]
    [InlineData(AppointmentStatus.CANCELADA, AppointmentStatus.CONFIRMADA)]
    [InlineData(AppointmentStatus.NO_ASISTIO, AppointmentStatus.PROGRAMADA)]
    public void ValidateTransition_RejectsInvalidTransitions(AppointmentStatus from, AppointmentStatus to)
    {
        var result = AppointmentPolicy.ValidateTransition(from, to);

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_APPOINTMENT_TRANSITION", result.Error.Code);
        Assert.Equal(409, result.Error.HttpStatus);
    }

    [Fact]
    public void ValidateTransition_RejectsSameStatus()
    {
        var result = AppointmentPolicy.ValidateTransition(AppointmentStatus.CONFIRMADA, AppointmentStatus.CONFIRMADA);

        Assert.True(result.IsFailure);
        Assert.Equal("SAME_APPOINTMENT_STATUS", result.Error.Code);
        Assert.Equal(409, result.Error.HttpStatus);
    }

    [Theory]
    [InlineData("2026-11-20T10:15:00-06:00", 30, true)]
    [InlineData("2026-11-20T09:45:00-06:00", 30, true)]
    [InlineData("2026-11-20T10:30:00-06:00", 30, false)]
    [InlineData("2026-11-20T09:30:00-06:00", 30, false)]
    public void HasOverlap_UsesHalfOpenIntervalsAndAllowsContiguousSlots(
        string candidateStartGmt6,
        int durationMinutes,
        bool expectedOverlap)
    {
        var existingStart = DateTimeOffset.Parse("2026-11-20T10:00:00-06:00").ToUniversalTime();
        var (existing, _) = Appointment.Create(
            Guid.NewGuid(),
            TestFixtures.NorteTenantId,
            TestFixtures.Pat0101Id,
            TestFixtures.MendozaNorteId,
            existingStart,
            30,
            "Control",
            TestFixtures.AdminNorteId,
            "Admin",
            DateTimeOffset.UtcNow);

        var candidateStart = DateTimeOffset.Parse(candidateStartGmt6).ToUniversalTime();
        var candidateEnd = candidateStart.AddMinutes(durationMinutes);

        var hasOverlap = AppointmentPolicy.HasOverlap(candidateStart, candidateEnd, [existing]);

        Assert.Equal(expectedOverlap, hasOverlap);
    }

    [Fact]
    public void HasOverlap_IgnoresCancelledAndNoShowAppointments()
    {
        var existingStart = DateTimeOffset.Parse("2026-11-20T10:00:00-06:00").ToUniversalTime();
        var (cancelled, _) = Appointment.Create(
            Guid.NewGuid(),
            TestFixtures.NorteTenantId,
            TestFixtures.Pat0101Id,
            TestFixtures.MendozaNorteId,
            existingStart,
            30,
            "Control cancelado",
            TestFixtures.AdminNorteId,
            "Admin",
            DateTimeOffset.UtcNow);
        cancelled.TransitionStatus(AppointmentStatus.CANCELADA, TestFixtures.AdminNorteId, "Admin", null, DateTimeOffset.UtcNow);

        var hasOverlap = AppointmentPolicy.HasOverlap(
            existingStart,
            existingStart.AddMinutes(30),
            [cancelled]);

        Assert.False(hasOverlap);
    }

    [Theory]
    [InlineData("2026-11-20T10:00:00-06:00", 9, "Control", "INVALID_SCHEDULE_TIME")]
    [InlineData("2026-11-20T10:00:00-06:00", 481, "Control", "INVALID_SCHEDULE_TIME")]
    [InlineData("2026-11-20T10:00:00Z", 30, "Control", "INVALID_SCHEDULE_TIME")]
    [InlineData("2026-11-20T10:00:00-06:00", 30, "   ", "INVALID_APPOINTMENT_DATA")]
    public void ValidateScheduleInput_EnforcesBoundsAndMandatoryFields(
        string startGmt6,
        int duration,
        string reason,
        string expectedErrorCode)
    {
        var result = AppointmentPolicy.ValidateScheduleInput(startGmt6, duration, reason);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedErrorCode, result.Error.Code);
        Assert.Equal(400, result.Error.HttpStatus);
    }
}
