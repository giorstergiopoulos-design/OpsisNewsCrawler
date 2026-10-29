namespace OpsisNewsCrawler.Windows;

/// <summary>
/// What the two live symbols of the animated icon show: the current weather of the first weather city (the same picture the bar draws)
/// and the highest level of an active alert (severe weather or a natural disaster). The icon frames are drawn again when this changes.
/// </summary>
public static class LiveIconState
{
    private static int weatherIcon = 1;   // WeatherKind, +10 at night; 1 = partly cloudy until the first reading arrives
    private static int hazardLevel;
    private static string tip = "";

    /// <summary>Raised on whichever thread called <see cref="Set"/>, only when something really changed.</summary>
    public static event Action? Changed;

    /// <summary>WeatherKind (+10 at night) of the first weather city.</summary>
    public static int WeatherIcon => weatherIcon;

    /// <summary>0 = nothing, 1 = information (green), 2 = orange, 3 = red.</summary>
    public static int HazardLevel => hazardLevel;

    /// <summary>A short text for the tooltip of the tray icon ("Athens 24 °C · Earthquake M5.1 Crete").</summary>
    public static string Tip => tip;

    public static void Set(int weatherIcon, int hazardLevel, string tip)
    {
        weatherIcon = Math.Clamp(weatherIcon, 0, 19);
        hazardLevel = Math.Clamp(hazardLevel, 0, 3);
        var changed = weatherIcon != LiveIconState.weatherIcon || hazardLevel != LiveIconState.hazardLevel;
        LiveIconState.weatherIcon = weatherIcon;
        LiveIconState.hazardLevel = hazardLevel;
        var tipChanged = !string.Equals(tip, LiveIconState.tip, StringComparison.Ordinal);
        LiveIconState.tip = tip;
        if (changed || tipChanged) Changed?.Invoke();
    }
}
