namespace TraderAlgoApi.Services.Session;

public sealed class NyseSessionService
{
    private static readonly TimeZoneInfo Eastern = GetEasternTimeZone();
    private static readonly TimeOnly SessionOpen = new(9, 30);
    private static readonly TimeOnly SessionClose = new(16, 0);

    public (DateTimeOffset Start, DateTimeOffset End) CurrentSession(DateTimeOffset utcNow)
    {
        var etNow = TimeZoneInfo.ConvertTime(utcNow, Eastern);
        return BuildSession(FindCurrentSessionDate(etNow));
    }

    public (DateTimeOffset Start, DateTimeOffset End) PreviousSession(DateTimeOffset utcNow)
    {
        var etNow = TimeZoneInfo.ConvertTime(utcNow, Eastern);
        var current = FindCurrentSessionDate(etNow);
        return BuildSession(FindPreviousWeekday(current));
    }

    private static DateOnly FindCurrentSessionDate(DateTimeOffset etNow)
    {
        var date = DateOnly.FromDateTime(etNow.DateTime);
        var time = TimeOnly.FromDateTime(etNow.DateTime);

        // Weekday at or past session open = current session is today (in progress or just closed)
        if (etNow.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday && time >= SessionOpen)
            return date;

        return FindPreviousWeekday(date);
    }

    private static DateOnly FindPreviousWeekday(DateOnly from)
    {
        var date = from.AddDays(-1);
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            date = date.AddDays(-1);
        return date;
    }

    private (DateTimeOffset Start, DateTimeOffset End) BuildSession(DateOnly date)
    {
        var start = new DateTime(date.Year, date.Month, date.Day, SessionOpen.Hour, SessionOpen.Minute, 0);
        var end   = new DateTime(date.Year, date.Month, date.Day, SessionClose.Hour, SessionClose.Minute, 0);
        return (
            TimeZoneInfo.ConvertTimeToUtc(start, Eastern),
            TimeZoneInfo.ConvertTimeToUtc(end,   Eastern)
        );
    }

    private static TimeZoneInfo GetEasternTimeZone()
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById("Eastern Standard Time", out var tz))
            return tz;
        return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    }
}
