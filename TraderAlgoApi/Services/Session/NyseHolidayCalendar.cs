namespace TraderAlgoApi.Services.Session;

/// <summary>
/// Hardcoded US equity market holidays. Update annually.
/// Early-close days (day after Thanksgiving, Christmas Eve) are NOT listed here because
/// the session still opens; the close time change is not modelled at this precision.
/// </summary>
public static class NyseHolidayCalendar
{
    private static readonly HashSet<DateOnly> Holidays =
    [
        // 2025
        new DateOnly(2025, 1,  1),  // New Year's Day
        new DateOnly(2025, 1,  20), // MLK Day
        new DateOnly(2025, 2,  17), // Presidents' Day
        new DateOnly(2025, 4,  18), // Good Friday
        new DateOnly(2025, 5,  26), // Memorial Day
        new DateOnly(2025, 6,  19), // Juneteenth
        new DateOnly(2025, 7,  4),  // Independence Day
        new DateOnly(2025, 9,  1),  // Labor Day
        new DateOnly(2025, 11, 27), // Thanksgiving
        new DateOnly(2025, 12, 25), // Christmas

        // 2026
        new DateOnly(2026, 1,  1),  // New Year's Day
        new DateOnly(2026, 1,  19), // MLK Day
        new DateOnly(2026, 2,  16), // Presidents' Day
        new DateOnly(2026, 4,  3),  // Good Friday
        new DateOnly(2026, 5,  25), // Memorial Day
        new DateOnly(2026, 6,  19), // Juneteenth
        new DateOnly(2026, 7,  3),  // Independence Day (observed, Jul 4 is Saturday)
        new DateOnly(2026, 9,  7),  // Labor Day
        new DateOnly(2026, 11, 26), // Thanksgiving
        new DateOnly(2026, 12, 25), // Christmas

        // 2027
        new DateOnly(2027, 1,  1),  // New Year's Day
        new DateOnly(2027, 1,  18), // MLK Day
        new DateOnly(2027, 2,  15), // Presidents' Day
        new DateOnly(2027, 3,  26), // Good Friday
        new DateOnly(2027, 5,  31), // Memorial Day
        new DateOnly(2027, 6,  18), // Juneteenth (observed, Jun 19 is Saturday)
        new DateOnly(2027, 7,  5),  // Independence Day (observed, Jul 4 is Sunday)
        new DateOnly(2027, 9,  6),  // Labor Day
        new DateOnly(2027, 11, 25), // Thanksgiving
        new DateOnly(2027, 12, 24), // Christmas (observed, Dec 25 is Saturday)
    ];

    public static bool IsHoliday(DateOnly date) => Holidays.Contains(date);
}
