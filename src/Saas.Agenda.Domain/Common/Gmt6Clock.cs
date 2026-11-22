using System.Globalization;

namespace Saas.Agenda.Domain.Common;

public static class Gmt6Clock
{
    public static readonly TimeSpan FixedOffset = TimeSpan.FromHours(-6);

    private static readonly string[] StrictIsoFormats =
    [
        "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz",
        "yyyy-MM-dd'T'HH:mmzzz",
    ];

    public static string FormatIsoGmt6(DateTimeOffset timestamp)
    {
        return timestamp
            .ToOffset(FixedOffset)
            .ToString("yyyy-MM-ddTHH:mm:ss'-06:00'", CultureInfo.InvariantCulture);
    }

    public static string FormatDateGmt6(DateOnly date)
    {
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static string FormatTime(TimeOnly time)
    {
        return time.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    public static DateOnly TodayGmt6(DateTimeOffset utcNow)
    {
        return DateOnly.FromDateTime(utcNow.ToOffset(FixedOffset).DateTime);
    }

    public static (DateOnly Date, TimeOnly Time) ToGmt6DateAndTime(DateTimeOffset utcTimestamp)
    {
        var gmt6 = utcTimestamp.ToOffset(FixedOffset);
        return (DateOnly.FromDateTime(gmt6.DateTime), TimeOnly.FromDateTime(gmt6.DateTime));
    }

    public static DateTimeOffset CombineGmt6ToUtc(DateOnly date, TimeOnly time)
    {
        return new DateTimeOffset(
            date.Year,
            date.Month,
            date.Day,
            time.Hour,
            time.Minute,
            0,
            FixedOffset).ToUniversalTime();
    }

    public static bool TryParseStrictGmt6(string? input, out DateTimeOffset parsedUtc)
    {
        parsedUtc = default;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();
        if (!trimmed.EndsWith("-06:00", StringComparison.Ordinal))
        {
            return false;
        }

        if (!DateTimeOffset.TryParseExact(
                trimmed,
                StrictIsoFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dto))
        {
            return false;
        }

        if (dto.Offset != FixedOffset)
        {
            return false;
        }

        parsedUtc = dto.ToUniversalTime();
        return true;
    }

    public static bool TryParseGmt6Date(string? input, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        return DateOnly.TryParseExact(
            input.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }

    public static bool TryParseTimeOnly(string? input, out TimeOnly time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        return TimeOnly.TryParseExact(
            input.Trim(),
            "HH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out time);
    }

    public static (DateTimeOffset StartUtc, DateTimeOffset EndExclusiveUtc) ToUtcDayBounds(
        DateOnly fromDate,
        DateOnly toDate)
    {
        if (fromDate > toDate)
        {
            throw new ArgumentException("fromDate no puede ser mayor que toDate.");
        }

        var startGmt6 = new DateTimeOffset(fromDate.Year, fromDate.Month, fromDate.Day, 0, 0, 0, FixedOffset);
        var endExclusiveGmt6 = new DateTimeOffset(toDate.Year, toDate.Month, toDate.Day, 0, 0, 0, FixedOffset).AddDays(1);

        return (startGmt6.ToUniversalTime(), endExclusiveGmt6.ToUniversalTime());
    }
}
