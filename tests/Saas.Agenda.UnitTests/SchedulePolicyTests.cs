using Saas.Agenda.Domain.Common;
using Saas.Agenda.Domain.Entities;
using Saas.Agenda.Domain.Policies;
using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.UnitTests;

public sealed class SchedulePolicyTests
{
    [Fact]
    public void ValidateWeeklySlots_RejectsInvalidDayFormatDurationAndInternalOverlap()
    {
        var invalidDay = SchedulePolicy.ValidateWeeklySlots([("FUNDAY", "09:00", "12:00")]);
        Assert.Equal("INVALID_SCHEDULE_DAY", invalidDay.Error.Code);

        var invalidTime = SchedulePolicy.ValidateWeeklySlots([("MONDAY", "9:00", "12:00")]);
        Assert.Equal("INVALID_SCHEDULE_BLOCK", invalidTime.Error.Code);

        var tooShort = SchedulePolicy.ValidateWeeklySlots([("MONDAY", "09:00", "09:05")]);
        Assert.Equal("INVALID_SCHEDULE_BLOCK", tooShort.Error.Code);

        var overlap = SchedulePolicy.ValidateWeeklySlots([
            ("MONDAY", "09:00", "13:00"),
            ("MONDAY", "12:30", "16:00"),
        ]);
        Assert.Equal("OVERLAPPING_SCHEDULE_BLOCKS", overlap.Error.Code);

        var validSplit = SchedulePolicy.ValidateWeeklySlots([
            ("MONDAY", "16:00", "19:00"),
            ("MONDAY", "09:00", "15:00"),
        ]);
        Assert.True(validSplit.IsSuccess);
        Assert.Equal(new TimeOnly(9, 0), validSplit.Value[0].Start);
    }

    [Fact]
    public void ResolveEffectiveDaySchedule_RespectsExceptionThenSpecialistThenClinicThenUnconfigured()
    {
        var monday = new DateOnly(2026, 11, 23);

        var (clinic, _) = ClinicSchedule.Create(
            TestFixtures.NorteTenantId,
            [(ScheduleDayOfWeek.MONDAY, new TimeOnly(8, 0), new TimeOnly(20, 0))],
            TestFixtures.AdminNorteId,
            "Admin",
            DateTimeOffset.UtcNow);

        var (specCustom, _) = SpecialistSchedule.Create(
            Guid.NewGuid(),
            TestFixtures.NorteTenantId,
            TestFixtures.MendozaNorteId,
            useClinicFallback: false,
            [(ScheduleDayOfWeek.MONDAY, new TimeOnly(9, 0), new TimeOnly(14, 0))],
            TestFixtures.MendozaNorteId,
            "Mendoza",
            DateTimeOffset.UtcNow);

        var (specFallback, _) = SpecialistSchedule.Create(
            Guid.NewGuid(),
            TestFixtures.NorteTenantId,
            TestFixtures.SalazarNorteId,
            useClinicFallback: true,
            [],
            TestFixtures.AdminNorteId,
            "Admin",
            DateTimeOffset.UtcNow);

        var ex = SpecialistScheduleException.Create(
            Guid.NewGuid(),
            TestFixtures.NorteTenantId,
            TestFixtures.MendozaNorteId,
            monday,
            isUnavailable: true,
            null,
            null,
            "Congreso médico",
            TestFixtures.MendozaNorteId,
            "Mendoza",
            DateTimeOffset.UtcNow);

        var unconfigured = SchedulePolicy.ResolveEffectiveDaySchedule(monday, null, null, null);
        Assert.Equal(ScheduleSource.UNCONFIGURED, unconfigured.Source);
        Assert.True(unconfigured.IsUnavailable);

        var fromClinic = SchedulePolicy.ResolveEffectiveDaySchedule(monday, specFallback, clinic, null);
        Assert.Equal(ScheduleSource.CLINIC_FALLBACK, fromClinic.Source);
        Assert.Single(fromClinic.Blocks);
        Assert.Equal(new TimeOnly(8, 0), fromClinic.Blocks[0].StartTime);

        var fromSpec = SchedulePolicy.ResolveEffectiveDaySchedule(monday, specCustom, clinic, null);
        Assert.Equal(ScheduleSource.SPECIALIST_SCHEDULE, fromSpec.Source);
        Assert.Equal(new TimeOnly(14, 0), fromSpec.Blocks[0].EndTime);

        var fromException = SchedulePolicy.ResolveEffectiveDaySchedule(monday, specCustom, clinic, ex);
        Assert.Equal(ScheduleSource.EXCEPTION, fromException.Source);
        Assert.True(fromException.IsUnavailable);
    }

    [Fact]
    public void ValidateExceptionInput_RejectsPastDatesAndConflictingFlags()
    {
        var today = new DateOnly(2026, 11, 21);

        var past = SchedulePolicy.ValidateExceptionInput("2026-11-20", true, null, null, "Ayer", today);
        Assert.Equal("PAST_SCHEDULE_NOT_ALLOWED", past.Error.Code);

        var unavailableWithTimes = SchedulePolicy.ValidateExceptionInput("2026-11-22", true, "09:00", "12:00", "Festivo", today);
        Assert.Equal("INVALID_SCHEDULE_BLOCK", unavailableWithTimes.Error.Code);

        var availableValid = SchedulePolicy.ValidateExceptionInput("2026-11-22", false, "10:00", "13:00", "Guardia dominical", today);
        Assert.True(availableValid.IsSuccess);
        Assert.Equal(new TimeOnly(10, 0), availableValid.Value.CustomStartTime);
    }

    [Fact]
    public void ComputeAvailableSlots_FiltersPastAndBusyAppointments()
    {
        var date = new DateOnly(2026, 11, 20);
        var nowUtc = Gmt6Clock.CombineGmt6ToUtc(date, new TimeOnly(9, 15));

        var effective = new EffectiveDaySchedule(
            date,
            ScheduleSource.SPECIALIST_SCHEDULE,
            false,
            null,
            [
                new TimeBlock(new TimeOnly(9, 0), new TimeOnly(11, 0)),
                new TimeBlock(new TimeOnly(12, 0), new TimeOnly(13, 0)),
            ]);

        var (busyApt, _) = Appointment.Create(
            Guid.NewGuid(),
            TestFixtures.NorteTenantId,
            TestFixtures.Pat0101Id,
            TestFixtures.MendozaNorteId,
            Gmt6Clock.CombineGmt6ToUtc(date, new TimeOnly(10, 0)),
            30,
            "Ocupado",
            TestFixtures.AdminNorteId,
            "Admin",
            nowUtc);

        var freeSlots = SchedulePolicy.ComputeAvailableSlots(date, effective, [busyApt], 30, nowUtc);
        var freeStartTimes = freeSlots.Select(s => Gmt6Clock.FormatTime(s.StartTime)).ToArray();

        Assert.Equal(["09:30", "10:30", "12:00", "12:30"], freeStartTimes);
    }
}
