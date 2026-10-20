using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>The Help / Instructions / Version history / License texts, in the language of the UI (see locale files).</summary>
public static class AboutTexts
{
    public const string Version = "0.12.5";

    public static string Help => Loc.T("help.text");
    public static string Instructions => Loc.T("instructions.text");
    public static string History => Loc.T("history.text");
    public static string License => Loc.T("license.text");
}
