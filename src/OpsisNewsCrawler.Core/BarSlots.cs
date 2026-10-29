namespace OpsisNewsCrawler.Core;

/// <summary>What one part of a bar shows.</summary>
public enum SlotKind { News, Stocks, Weather, Hazards }

/// <summary>One half (or the whole) of a bar. <see cref="Category"/> is the id of a news category; null = the category that is active.</summary>
public readonly record struct BarSlot(SlotKind Kind, string? Category = null)
{
    /// <summary>"news", "news:&lt;category id&gt;", "stocks" or "weather" (the form saved in the configuration).</summary>
    public string ToSpec() => Kind switch
    {
        SlotKind.Stocks => "stocks",
        SlotKind.Weather => "weather",
        SlotKind.Hazards => "hazards",
        _ => string.IsNullOrWhiteSpace(Category) ? "news" : "news:" + Category
    };

    public static BarSlot? Parse(string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return null;
        spec = spec.Trim();
        if (spec.Equals("stocks", StringComparison.OrdinalIgnoreCase)) return new BarSlot(SlotKind.Stocks);
        if (spec.Equals("weather", StringComparison.OrdinalIgnoreCase)) return new BarSlot(SlotKind.Weather);
        if (spec.Equals("hazards", StringComparison.OrdinalIgnoreCase)) return new BarSlot(SlotKind.Hazards);
        if (spec.Equals("news", StringComparison.OrdinalIgnoreCase)) return new BarSlot(SlotKind.News);
        if (spec.StartsWith("news:", StringComparison.OrdinalIgnoreCase) && spec.Length > 5) return new BarSlot(SlotKind.News, spec[5..]);
        return null;
    }
}

/// <summary>What a bar carries: one part, or two parts side by side (the bar is split in the middle).</summary>
public sealed record BarSlots(BarSlot A, BarSlot? B)
{
    public IEnumerable<BarSlot> All()
    {
        yield return A;
        if (B is { } b) yield return b;
    }

    public bool Has(SlotKind kind) => All().Any(s => s.Kind == kind);

    /// <summary>The closest of the old four-value setting (for code that only needs to know "does this bar show news").</summary>
    public BarContent ToLegacy()
    {
        if (Has(SlotKind.News) && Has(SlotKind.Stocks)) return BarContent.Both;
        if (Has(SlotKind.News)) return BarContent.News;
        if (Has(SlotKind.Stocks)) return BarContent.Stocks;
        return BarContent.Weather;   // weather and hazards have no old equivalent
    }
}

public static class BarSpec
{
    public static BarSlots FromLegacy(BarContent content, bool stocksLeft) => content switch
    {
        BarContent.Stocks => new BarSlots(new BarSlot(SlotKind.Stocks), null),
        BarContent.Weather => new BarSlots(new BarSlot(SlotKind.Weather), null),
        BarContent.Both => stocksLeft
            ? new BarSlots(new BarSlot(SlotKind.Stocks), new BarSlot(SlotKind.News))
            : new BarSlots(new BarSlot(SlotKind.News), new BarSlot(SlotKind.Stocks)),
        _ => new BarSlots(new BarSlot(SlotKind.News), null)
    };

    /// <summary>The saved specs win; without them the old four-value setting is translated.</summary>
    public static BarSlots Resolve(string? a, string? b, BarContent legacy, bool stocksLeft)
    {
        if (BarSlot.Parse(a) is not { } first) return FromLegacy(legacy, stocksLeft);
        var second = BarSlot.Parse(b);
        if (second is { } s && s == first) second = null;
        return new BarSlots(first, second);
    }

    /// <summary>A spec whose news category no longer exists falls back to the active category; unknown text becomes "".</summary>
    public static string Clean(string? spec, Func<string, bool> categoryExists)
    {
        var slot = BarSlot.Parse(spec);
        if (slot is not { } s) return "";
        if (s.Kind == SlotKind.News && s.Category is { } c && !categoryExists(c)) return "news";
        return s.ToSpec();
    }
}
