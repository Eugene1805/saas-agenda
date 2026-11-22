using Saas.Agenda.Domain.Common;

namespace Saas.Agenda.UnitTests;

public sealed class Gmt6ClockTests
{
    [Theory]
    [InlineData("2026-11-20T10:00:00-06:00", 16, 0)]
    [InlineData("2026-11-20T23:30:00-06:00", 5, 30)]
    [InlineData("2026-11-20T12:00-06:00", 18, 0)]
    public void TryParseStrictGmt6_AcceptsValidGmt6Offsets_AndNormalizesToUtc(
        string input,
        int expectedUtcHour,
        int expectedUtcMinute)
    {
        var ok = Gmt6Clock.TryParseStrictGmt6(input, out var utc);

        Assert.True(ok);
        Assert.Equal(TimeSpan.Zero, utc.Offset);
        Assert.Equal(expectedUtcHour, utc.Hour);
        Assert.Equal(expectedUtcMinute, utc.Minute);
    }

    [Theory]
    [InlineData("")]
    [InlineData("2026-11-20T10:00:00")]
    [InlineData("2026-11-20T10:00:00Z")]
    [InlineData("2026-11-20T10:00:00+00:00")]
    [InlineData("2026-11-20T10:00:00-05:00")]
    [InlineData("not-a-date")]
    public void TryParseStrictGmt6_RejectsNonGmt6OrMalformedStrings(string input)
    {
        var ok = Gmt6Clock.TryParseStrictGmt6(input, out _);

        Assert.False(ok);
    }

    [Fact]
    public void ToUtcDayBounds_ConvertsGmt6CalendarRangeToUtcHalfOpenInterval()
    {
        var from = new DateOnly(2026, 11, 20);
        var to = new DateOnly(2026, 11, 20);

        var (startUtc, endExclusiveUtc) = Gmt6Clock.ToUtcDayBounds(from, to);

        Assert.Equal(DateTimeOffset.Parse("2026-11-20T06:00:00Z"), startUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-11-21T06:00:00Z"), endExclusiveUtc);
    }
}
