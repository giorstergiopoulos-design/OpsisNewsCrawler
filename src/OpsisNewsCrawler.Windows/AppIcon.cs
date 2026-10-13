using System.Reflection;

namespace OpsisNewsCrawler.Windows;

/// <summary>The application icon (embedded in this assembly), shared by the tray, the settings window and plugins.</summary>
public static class AppIcon
{
    private const string ResourceName = "OpsisNewsCrawler.app.ico";
    private static Icon? icon;

    public static Icon Get()
    {
        if (icon is not null) return icon;
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
            if (stream is not null) return icon = new Icon(stream);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            // fall through to the default icon
        }
        return icon = SystemIcons.Application;
    }
}
