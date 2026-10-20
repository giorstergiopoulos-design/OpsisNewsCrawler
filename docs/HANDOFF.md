# HANDOFF — Opsis News Crawler (written 2026-10-13, for a Claude Code session on Windows)

Read `CLAUDE.md` first. Owner's own words for the open requirements are in `docs/USER-REQUIREMENTS-2026-10-13.md`
(last 13 lines of that file = the requirement list; the Python code above them is a sample the owner found).

## 0. How to start (Windows, Claude Code)
1. Unzip, open the folder in Claude Code, `dotnet build OpsisNewsCrawler.sln -c Release`.
2. Fix compile errors first (the code of everything marked **NEW (unbuilt)** below was never compiled).
3. Run `bin\OpsisNewsCrawler.exe` (via `Run-OpsisNewsCrawler-Build.bat` option 1) and test each requirement on a real desktop,
   including Windows 11 + an auto-hide taskbar + a second monitor.

## 1. Status of what exists
**BUILT AND RUN BY THE OWNER (v0.7.0, compiled OK on his machine, behaviour only partly confirmed):**
smooth time-based scrolling, pause on hover, click opens article, mouse-wheel changes speed, hover preview card
(picture, source, time, summary), source icons (favicon or letter badge), RSS reader (RSS 2.0 + Atom), 4 built-in modes
(Economy/Market/International/Greece) with fixed feed lists, settings window (bilingual), tray icon + menu,
single instance, right-click on the strip → settings (standalone) or `NavigationRequested` event (plugin), app icon,
installer (Inno Setup 6.7) with uninstall, `.bat` menu (build / installer / uninstall).

**Known behaviour gaps reported by the owner on v0.7.0 → these are the open requirements below.**

## 2. Requirements (owner, 2026-10-13) with status and implementation plan
Status key: TODO = not started · NEW (unbuilt) = code written in the last session, never compiled · see section 4 for the final table.

| # | Requirement (owner's words, translated) | Plan |
|---|---|---|
| R1 | Live Euronext Athens prices, free; other Euronext exchanges selectable by the user | Provider abstraction `IQuoteProvider`. Athens → `AthensDelayedFileProvider` (official 15-min delayed CSV, see `DATA-SOURCES.md`). Other exchanges → opt-in `GoogleFinanceQuoteProvider` (unofficial). Settings tab "Αγορές": tick exchanges, edit watchlist lines `SYMBOL;ISIN;Name` (Athens) or `TICKER:EXCH` (Google). Quote items are drawn in the strip with colour + arrow; delayed label. |
| R2 | When the taskbar auto-hides, the crawler must follow it (the existing setting does nothing) | Poll the taskbar window rect (`FindWindow("Shell_TrayWnd")` + `GetWindowRect`, secondary taskbars `Shell_SecondaryTrayWnd`) every ~30 ms while auto-hide + follow are on; bar Y = monitor.Bottom − visibleTaskbarHeight − barHeight. `SHAppBarMessage(ABM_GETSTATE)` tells whether auto-hide is on. |
| R3 | Maximized windows must stop at the TOP of the crawler bar | Register the bar as an **AppBar** (`ABM_NEW`, `ABM_QUERYPOS`, `ABM_SETPOS`; edge bottom/top) so Windows shrinks the work area. Handle `ABN_POSCHANGED`/`ABN_FULLSCREENAPP`. **Must `ABM_REMOVE` on exit/hide/mode change.** Only for Bottom/Top positions; setting "Reserve screen space". |
| R4 | In Custom position show the position settings: by mouse and by screen coordinates | Settings: X, Y, Width numeric boxes (+ monitor selector) and a "Move with mouse" edit mode: `WM_NCHITTEST` returns `HTCAPTION` (drag) / `HTLEFT`/`HTRIGHT` (resize), canvas returns `HTTRANSPARENT`; `WM_EXITSIZEMOVE` → save bounds. |
| R5 | International news are English BBC although sites exist: user must choose websites per category or define a personal category of sites/topics | Config `Categories[]` (built-in + user-defined) each with `Feeds[]` (name, url, enabled). Settings tab "Πηγές": category list, add/rename/delete category, feed checklist, add feed by URL, **add by website URL with RSS auto-discovery** (`<link rel="alternate" type="application/rss+xml">` then /feed,/rss.xml…), presets list. Greek international default: `https://www.in.gr/world/feed/`. |
| R6 | Light/Dark mode and transparency selection ("copy the mechanism of Opsis, which was added later") | `ThemeMode {System, Light, Dark}` + opacity 30–100 %. **The Opsis code was never seen** — expose `IOpsisNewsCrawlerPlugin.SetTheme(string theme, double opacity)` so Opsis can push its own theme, and keep all colours in one `Theme` class so they can be swapped for Opsis tokens. Ask the owner for the Opsis theme source/screenshot. |
| R7 | When a site's icon is dark or monochrome, render it automatically so it is visible on the bar | After decoding a favicon compute mean luminance (alpha-weighted) and saturation. If contrast with the bar background is low: monochrome → invert via `ColorMatrix`; else draw on a contrasting rounded plate. Fall back to `<link rel=icon|apple-touch-icon>` from the home page when `/favicon.ico` fails. |
| R8 | The crawler's windows must look like Opsis | Same `Theme` applied to Settings, preview card, dialogs; dark title bar via `DwmSetWindowAttribute(DWMWA_USE_IMMERSIVE_DARK_MODE=20)`. Needs Opsis tokens (see R6). |
| R9 | An article that has been read must not show again in the bar | `ReadStore` (`%AppData%\OpsisNewsCrawler\read.json`, URL → timestamp, pruned after 14 days). Click marks read, removes it from the strip immediately (keeping scroll offset); refresh filters read URLs; option "Hide read" + "Reset read list". |
| R10 | Integrate the bar into the taskbar: show it in the empty space after the pinned apps, with the taskbar's colours and transparency, not looking foreign | Mode `TaskbarIntegrated`: topmost borderless window over the taskbar free span. Free span = larger of [left of first icon] / [right of last icon … left of tray] measured with UI Automation (`System.Windows.Automation`, needs `<UseWPF>true</UseWPF>` in the Windows csproj) on a background task every ~2 s; fallback = configurable offsets. Colours from registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize` (`SystemUsesLightTheme`, `EnableTransparency`, `ColorPrevalence`). Optional experimental Mica/Acrylic via `DwmSetWindowAttribute(DWMWA_SYSTEMBACKDROP_TYPE=38)` + `DwmExtendFrameIntoClientArea(-1)` with black background. Re-assert TOPMOST ~1/s because the taskbar steals z-order. **Experimental: Windows 11 only guaranteed to need tuning.** |
| R11 | The bar must not appear in front of video players, browsers or apps when they are fullscreen | `SHQueryUserNotificationState` (2=BUSY, 3=D3D fullscreen, 4=presentation) **and** geometric check (foreground window rect ⊇ monitor bounds, not Progman/WorkerW) in the 250 ms timer; hide the form + preview while true, show again after. Also `ABN_FULLSCREENAPP`. |
| R12 | As a plugin, don't show an extra small app icon in the taskbar/tray; instead add the options to the matching Opsis icon | The plugin never creates a `NotifyIcon`; add `GetTrayMenuItems()` to the plugin contract (`PluginMenuItem(key, titleEl, titleEn, action)`) so Opsis can append them to its own tray menu. Standalone keeps its own tray icon. |
| R0 | (done earlier) right-click on the strip opens settings / the Opsis tab | done in v0.7.0 |

## 3. Decisions the owner should know about
- Google Finance scraping from the sample was **not** ported as default: sample URL is broken, Athens tickers are not found on Google, ToS forbids automated access, EU consent wall. Offered only as opt-in.
- Yahoo / Stooq / Euronext-live endpoints deliberately not used (robots.txt / encrypted).
- Athens prices come from MiFID post-trade transparency files: **last trade price per ISIN, 15 min delayed**, no names → ISIN mapping is user-editable; change % needs a previous close (previous file / local cache).
- Opsis theme mechanism and Opsis tray-menu API are unknown to this project → contract methods were added (`SetTheme`, `GetTrayMenuItems`, `NavigationRequested`) and must be wired on the Opsis side.

## 4. Final status table
(see the end of this file — updated when the session ended)

### Update 2026-10-13 (Claude Code on Windows)
- Build is green (0 errors). Plugin output folder renamed to `bin\plugins\Opsis - Desktop Customization\`.
- R11 DONE, UNTESTED: `Native.IsFullscreenAppActive` (SHQueryUserNotificationState + geometry without title bar); `TickerWindow.CheckForeground` hides/shows the strip.
- R9 DONE, UNTESTED: `Core/ReadStore.cs`, `TickerHost.ShowItems/ResetReadList`, `AppConfig.HideRead`, settings checkbox + reset button.
- Next easiest: R7 (favicon contrast), R4 (custom position boxes), R2 (taskbar auto-hide), R3 (AppBar).

## 5. Status at the end of the 2026-10-13 evening session
Read `docs/TASKS.md` for the per-task state; this is the summary.
- Requirements R1–R12 of section 2 are implemented except the other Euronext exchanges (R1). Verified live on the owner's PC: R3 (AppBar shrinks the work area 1400→1358 and restores on exit), R6, R8, R10 (bar in the taskbar free space, measured with MSAA), R11 (bar hides under a fullscreen window and returns). Written but not exercised: R2 (auto-hide taskbar is off), R4 mouse drag, R7, R9 (a click opens the browser), R12 tray items.
- New owner requirements N1–N11 (`docs/USER-REQUIREMENTS-2026-10-13b.md`) are implemented: clear taskbar bar, Opsis-style settings, two bars + split, breaking news + live preview, alpha rendering with transparency/fade, Opsis About section, commodities, single-language UI, bar colours, backdrop presets.
- Opsis side (repo MotionDeskStudio, uncommitted): `src/Services/PluginHost.cs`, nav page, tray items, About section, NoSnap/DeskStrip fixes, `libs\OpsisNewsCrawler.Plugin.Abstractions.dll`, locale keys `About.SectionPlugins`. It builds (0 errors) and was run with the plugin in a scratch folder.
- Next steps: commit both repos when the owner agrees; test auto-hide taskbar, mouse placement and the Opsis tray items by hand; add other Euronext exchanges (opt-in provider); more languages (copy `Core/Locales/en.json` to `locales\<code>.json`); live feed: YouTube channel handle links (@name) are not resolved yet (video, /live/ and /channel/UC… links are).


## 6. Update 2026-10-15 (read this before continuing)
Version is 0.11.5. The latest source was edited without any compiler or runtime: expect a few compile errors and fix them first.
Changed files: `Core/PythHermes.cs` (new), `Core/Quotes.cs` (`FredEnergyProvider` = Pyth + FRED, `SourceStatus`/`QuoteService.Statuses()`), `Core/QuoteRefreshPolicy.cs`, `Windows/SettingsPanel.cs` (`Combine`, `BuildInfo`, status card, `PreviewTabs = {1,2}`), tests `PythHermesTests` / `QuoteRefreshPolicyTests`, docs.
Decisions: Pyth data are index / front-month reference prices, not the NYMEX/ICE tick (say so if the UI ever labels them); Athens stays 15 min delayed (no free live source exists); Yahoo/Stooq stay forbidden. Also present (reviewed by reading only): `Core/PriceAlerts.cs`, `Windows/GlobalHotkey.cs`, `Windows/StartupRegistration.cs`, first-run settings, feed test. Remaining work list: `docs/TASKS.md` round 15, last bullet.


## 7. Update 2026-10-17 — version 0.11.5 (still NO compiler: nothing was built or run)
- Settings = 3 top tabs (`FlatStrip`, large) Settings / Sources / Information, each with a row of sub-buttons (`FlatStrip`, small). `SettingsPanel.AddGroup`, `leaves` list: `--tab N` / `SelectTab(N)` / `TabCount` count sub-pages in order: 0 General, 1 Bars, 2 Position, 3 Desktop bar, 4 Look, 5 Breaking, 6 News flow, 7 Markets flow, 8 Weather flow, 9.. Help, Instructions, History, License, Updates. Bar preview shows on sub-pages 1, 2, 4, 5 (`PreviewSubs`). The old `FlatTabs` rail class is still in `FlatControls.cs` but unused (delete when convenient).
- Bar content model: `Core/BarSlots.cs` (`BarSlot`, `BarSlots`, `BarSpec`). Config: `MainSlotA/B`, `SecondSlotA/B`, `MiniSlotA/B`, `MonitorBar.SlotA/B` (spec strings `news`, `news:<categoryId>`, `stocks`, `weather`); empty = the old `MainContent`/`SecondContent`/`SplitStocksOnLeft` apply. `TickerHost.AllBarSlots/PushTo/ItemsFor`; news of non-active categories: `extraNews*`, `RefreshExtraNewsAsync` (own `RssNewsProvider`), cached as `news-<id>`.
- Desktop bar = the old mini weather bar (config names `MiniWeather*` kept). UI in `BuildDesktopBar`. Move mode: `TickerWindow.SetEditMode` warps the pointer onto the bar (`SnapCursorToBar`), `WM_SETCURSOR` shows `SizeAll`, a desktop-layer bar goes topmost while edited.
- Weather detail fields: `AppConfig.WeatherFields` (`wind, gusts, humidity, feels, rain, cloud, pressure`), `WeatherReading` got optional fields, Open-Meteo `current=` asks for them. Cache from older versions simply lacks them until the next fetch.
- Offline / provider-down: `Core/DataCache.cs`, `TickerHost.Notice/WithNotice`, keys `net.*`. Breaking: `breaking.now` ("ΣΥΜΒΑΙΝΕΙ ΤΩΡΑ"). Athens "15 min" label removed from the bar (stated in Help and in the Markets flow note).
- Core review fixes applied: `QuoteStore` lock, Pyth `expo <= 0` only, cancellation checks and `ConcurrentDictionary` in `FredEnergyProvider`, day change for live energy prices from the last FRED value.
- Not done: FX more often than daily (the ECB publishes once per working day; no verified free live FX source), yfinance (Python library scraping Yahoo, forbidden by its ToS, not usable from .NET).
- FIRST on Windows: `dotnet build`, fix compile errors (this round touched `SettingsPanel`, `TickerHost`, `BarPreview`, `TickerWindow`, `FlatControls`, `AppConfig`, `Weather`), `dotnet test`, then walk through every sub-page.


## 8. Update 2026-10-19 — version 0.11.5.2 (still NO compiler)
- Alert sound: `Windows/AlertSound.cs` (WAV built in memory, `SoundPlayer`, falls back to `SystemSounds.Exclamation`); `TickerHost.PlayAlertSound` is called from `StartBreaking`, from new urgent headlines while already active, and from weather alerts. Setting: `AppConfig.BreakingSound`.
- Severe weather for the user's area: `Core/WeatherAlerts.cs` (pure rules + `WeatherAlertService`, Open-Meteo hourly `forecast_hours=12`), `AppConfig.WeatherAlertsEnabled/AlertCityId/ResolveAlertCity()`, `TickerHost.CheckWeatherAlertsAsync` (every 30 min, shows a warning once until the condition passes), UI in Settings → Breaking, tests `WeatherAlertsTests`. These are computed from the forecast, NOT the official EMY bulletin (no verified free official feed was reachable; meteoalarm terms not verified). Do not label them "official".
- `DetectBreaking` now keeps link-less alert items (price / weather) when the urgent news list is refreshed.


## 9. Update 2026-10-19 — version 0.11.5.3 (still NO compiler)
- Weather + map window: click a city on a weather line → `Windows/WeatherMapForm.cs` (WebView2, `NavigateToString`, Leaflet 1.9.4 from unpkg without SRI, OpenStreetMap tiles with a Referer header set in `WebResourceRequested`; the OSM tile policy asks for identification and light use). Wiring: `NewsItem.Key` (= city id, set in `WeatherInfo.ToItem`), `TickerCanvas.WeatherClicked`, `TickerWindow.WeatherRequested`, `TickerHost.ShowWeatherMap`. Setting `AppConfig.WeatherMapOnClick` (Sources → Weather flow). Source: the user's `weather_ticker_guide.md` (its hard-coded Naxos / per-call HttpClient / unescaped HTML were NOT copied; the existing readings are reused).
- Open question to verify on Windows: does the OSM tile server accept the Referer set this way; does Leaflet load inside `NavigateToString` pages.
