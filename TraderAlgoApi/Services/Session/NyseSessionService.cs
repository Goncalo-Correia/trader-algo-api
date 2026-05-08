namespace TraderAlgoApi.Services.Session;

public sealed class NyseSessionService
{
    private static readonly TimeZoneInfo Eastern = GetEasternTimeZone();
    private static readonly TimeOnly SessionOpen = new(9, 30);
    private static readonly TimeOnly SessionClose = new(16, 0);

    public bool IsMarketOpen(DateTimeOffset utcNow)
    {
        var etNow = TimeZoneInfo.ConvertTime(utcNow, Eastern);
        var date  = DateOnly.FromDateTime(etNow.DateTime);
        var time  = TimeOnly.FromDateTime(etNow.DateTime);

        return IsMarketDay(date) && time >= SessionOpen && time < SessionClose;
    }

    public bool IsMarketDay(DateOnly date) =>
        date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday
        && !NyseHolidayCalendar.IsHoliday(date);

    /// <summary>
    /// Returns the next session open time in UTC. Returns <paramref name="utcNow"/> if the
    /// market is already open.
    /// </summary>
    public DateTimeOffset NextMarketOpen(DateTimeOffset utcNow)
    {
        if (IsMarketOpen(utcNow))
            return utcNow;

        var etNow = TimeZoneInfo.ConvertTime(utcNow, Eastern);
        var date  = DateOnly.FromDateTime(etNow.DateTime);
        var time  = TimeOnly.FromDateTime(etNow.DateTime);

        // If today is a market day and we are before the open, open is today.
        if (IsMarketDay(date) && time < SessionOpen)
            return BuildSession(date).Start;

        // Otherwise advance to the next market day.
        var next = date.AddDays(1);
        while (!IsMarketDay(next))
            next = next.AddDays(1);

        return BuildSession(next).Start;
    }

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
