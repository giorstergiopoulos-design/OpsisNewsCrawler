namespace OpsisNewsCrawler.Core;

public enum TickerMode { Economy, Market, International, Greece, Defence, Sports }
public enum TickerPosition { BottomTaskbar, Top, Center, Custom, TaskbarIntegrated }
public readonly record struct TickerScrollState(double Offset);
public enum NewsItemKind { News, Quote, Weather }

/// <summary>What a bar shows: news, market quotes, or both side by side (the bar is split in the middle).</summary>
public enum BarContent { News, Stocks, Both, Weather }

/// <summary>
/// One entry of the strip. <see cref="Kind"/> = Quote for market prices (Source = symbol, Title = price text,
/// Trend = +1 up / -1 down / 0 unchanged or unknown); Breaking = the headline is an urgent one.
/// </summary>
public sealed record NewsItem(string Title, string Url, string Source, DateTimeOffset PublishedAt, string? Summary = null, string? ImageUrl = null,
    NewsItemKind Kind = NewsItemKind.News, int Trend = 0, bool Breaking = false, int Icon = 0, string? Key = null);
/// <summary>A (delayed) price. ChangePercent is null when there is no reference price; ChangeFromOpen = measured against the first trade of the day instead of the previous close.</summary>
public sealed record MarketQuote(string Symbol, string Name, decimal Price, decimal? ChangePercent, DateTimeOffset AsOf, bool Delayed = true, bool ChangeFromOpen = false);

/// <summary>
/// Holds the ticker state (mode, scroll position, pause flag). Must be used from the UI thread.
/// </summary>
public sealed class TickerState
{
    public TickerMode Mode { get; private set; } = TickerMode.Economy;
    public double ScrollOffset { get; private set; }

    /// <summary>Second scroll position, used by the right half when a bar is split in two.</summary>
    public double ScrollOffsetB { get; private set; }
    public void SetScrollOffsetB(double offset) => ScrollOffsetB = double.IsFinite(offset) ? offset : 0;
    public bool Paused { get; private set; }
    public DateTimeOffset? LastNewsRefresh { get; private set; }
    public void SetMode(TickerMode mode) => Mode = mode;
    public void SetScrollOffset(double offset) => ScrollOffset = double.IsFinite(offset) ? offset : 0;
    public TickerScrollState CaptureScroll() => new(ScrollOffset);
    public void RestoreScroll(TickerScrollState state) => ScrollOffset = double.IsFinite(state.Offset) ? state.Offset : 0;
    public void Pause() => Paused = true;
    public void Resume() => Paused = false;
    public void MarkNewsRefresh() => LastNewsRefresh = DateTimeOffset.UtcNow;
}

/// <summary>
/// Converts the display strings used by the UI (e.g. "Αγορές / Markets") into enum values.
/// </summary>
public static class TickerParsing
{
    public static TickerMode ParseMode(string? text)
    {
        if (Enum.TryParse<TickerMode>(text, true, out var exact)) return exact;
        var t = (text ?? string.Empty).ToLowerInvariant();
        if (t.Contains("market") || t.Contains("αγορ")) return TickerMode.Market;
        if (t.Contains("international") || t.Contains("διεθν")) return TickerMode.International;
        if (t.Contains("greece") || t.Contains("ελλάδ") || t.Contains("ελλαδ")) return TickerMode.Greece;
        if (t.Contains("defen") || t.Contains("diplom") || t.Contains("άμυν") || t.Contains("αμυν") || t.Contains("διπλωμ")) return TickerMode.Defence;
        if (t.Contains("sport") || t.Contains("αθλητ")) return TickerMode.Sports;
        return TickerMode.Economy;
    }

    public static TickerPosition ParsePosition(string? text)
    {
        var t = (text ?? string.Empty).ToLowerInvariant();
        if (t.Contains("integrat") || t.Contains("ενσωματ")) return TickerPosition.TaskbarIntegrated;
        if (t.Contains("top") || t.Contains("πάνω") || t.Contains("πανω")) return TickerPosition.Top;
        if (t.Contains("center") || t.Contains("κέντρο") || t.Contains("κεντρο")) return TickerPosition.Center;
        if (t.Contains("custom") || t.Contains("προσαρμ")) return TickerPosition.Custom;
        return TickerPosition.BottomTaskbar;
    }
}
