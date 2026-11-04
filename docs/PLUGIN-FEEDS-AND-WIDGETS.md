# Plugin feeds and widgets (contract extension, 0.20.1)

Source of the request: the plugin contract note (PLUGIN-CONTRACT-2026-11.md, in the shared notes folder) (the "San simera" / Chronikologio plugin). The assembly version of `OpsisNewsCrawler.Plugin.Abstractions` stays **0.8.0.0** (Opsis loads the contract DLL in its default context; a plugin that asks for a newer assembly version cannot be bound). Everything added is optional: a plugin that does not implement it keeps working.

## New public types (`Plugin.Abstractions/PluginContract.cs`)
| Type | Purpose |
|---|---|
| `PluginWidget(Key, TitleEl, TitleEn, CreateContent, ReplacesBuiltIn?)` | A widget for the Opsis desktop surface; `ReplacesBuiltIn` (for example `"calendar"`) names the built-in widget it replaces. |
| `IPluginWidgetProvider` | A plugin implements it to offer widgets; the host checks with `is`. |
| `PluginFeedItem(Text, Url?)` | One line of a feed; the link opens when the line is clicked. |
| `PluginFeed(Key, TitleEl, TitleEn, GetItems)` | A text feed; `GetItems` is called on the UI thread. |
| `PluginFeeds` | Static registry (`Register`, `Unregister`, `All`, `Find`, event `Changed`), shared by the plugin and the News Crawler through the one contract DLL. |

## Who does what
- **Plugin**: registers its feeds with `PluginFeeds.Register(...)` (and may implement `IPluginWidgetProvider`).
- **News Crawler**: `SlotKind.Feed` (spec `feed:<key>`) in `Core/BarSlots.cs`; the bar part chooser in Settings > Bars lists every registered feed after "Natural events"; `TickerHost.PluginFeedForDisplay` turns the items into ticker lines (source = the feed title; an exception in the plugin gives an empty bar and a log line, never a crash) and the bars are pushed again when `PluginFeeds.Changed` is raised. A feed counts as text like news for the old four-value setting (`BarSlots.ToLegacy`).
- **Opsis host**: `PluginHost.FindWidgetReplacing`, `WidgetEngine` (see section 3 of the contract note; not part of this repository).

## Compatibility
`tests/OpsisNewsCrawler.Plugin.Tests` loads the plugin's own copy of the contract (`reference/OpsisNewsCrawler.Plugin.Abstractions.san-shmera.dll`, 22 016 bytes, SHA-256 `F3074836696EAD48ACC38F9E1E483BF0F3636EFE7A483C3E841AF47FF6374D63`) in its own load context and checks that every public type and member signature of it exists in the contract built here, that the assembly version is 0.8.0.0, and that the built DLL contains the text `PluginFeeds` (the check a host can do: an old 15 KB contract DLL lacks it and the plugin fails with "Could not load type IPluginWidgetProvider").

## Tray menu inside Opsis (0.20.2)
`GetTrayMenuItems` offers Settings, Refresh, Pause / Resume, Close and Open (`OpsisNewsCrawlerPlugin.BuildTrayMenu`). It does **not** offer `news.welcome` (welcome guide) or `news.suite` (the Opsis family): Opsis provides those once for the whole family, so they must not appear several times. The standalone program keeps both entries in its own tray menu. Tested in `TrayMenuTests`.

## Trust
Opsis asks for the SHA-256 of a plugin DLL before it loads it (`%AppData%\Opsis\plugin-trust.json`); this does not change.

## Not verified here
Loading the plugin inside a real Opsis 2.4.3, and the widget replacement (those are Opsis-side).
