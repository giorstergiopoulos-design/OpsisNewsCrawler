namespace OpsisNewsCrawler.Core;

/// <summary>
/// Chooses a conservative polling interval based on the freshness of enabled quote sources.
/// This reduces needless requests when only end-of-day or daily data is selected.
/// </summary>
public static class QuoteRefreshPolicy
{
    public const int LiveSeconds = 60;
    public const int AthensClosedSeconds = 15 * 60;
    public const int EndOfDaySeconds = 60 * 60;
    public const int DailySeconds = 6 * 60 * 60;

    /// <summary>
    /// Returns an interval that never polls faster than the user's configured interval.
    /// Athens delayed prices are polled more frequently during the local weekday session;
    /// Euronext end-of-day and ECB daily sources use slower intervals. Energy (Pyth Hermes) keeps the configured interval.
    /// </summary>
    public static int GetIntervalSeconds(
        IEnumerable<QuoteInstrument>? instruments,
        int configuredSeconds,
        DateTimeOffset now,
        TimeZoneInfo? marketTimeZone = null)
    {
        var baseline = Math.Clamp(configuredSeconds, 30, 3600);
        var enabled = (instruments ?? Enumerable.Empty<QuoteInstrument>())
            .Where(x => x is not null && x.Enabled)
            .Select(x => x.Source)
            .ToHashSet();
        if (enabled.Count == 0) return baseline;

        if (enabled.Contains(QuoteSource.Metals) || enabled.Contains(QuoteSource.Crypto) || enabled.Contains(QuoteSource.Energy))
            return baseline;

        var recommended = int.MaxValue;
        if (enabled.Contains(QuoteSource.Athens))
        {
            var zone = marketTimeZone ?? GetAthensTimeZone();
            var local = TimeZoneInfo.ConvertTime(now, zone);
            var inSession = local.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
                && local.TimeOfDay >= TimeSpan.FromHours(10)
                && local.TimeOfDay < TimeSpan.FromHours(18);
            recommended = Math.Min(recommended, inSession ? LiveSeconds : AthensClosedSeconds);
        }
        if (enabled.Contains(QuoteSource.Euronext))
            recommended = Math.Min(recommended, EndOfDaySeconds);
        if (enabled.Contains(QuoteSource.Currency))
            recommended = Math.Min(recommended, DailySeconds);

        return recommended == int.MaxValue ? baseline : Math.Max(baseline, recommended);
    }

    private static TimeZoneInfo GetAthensTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Athens"); }
        catch (TimeZoneNotFoundException)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("GTB Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.Local; }
        }
        catch (InvalidTimeZoneException) { return TimeZoneInfo.Local; }
    }
}
