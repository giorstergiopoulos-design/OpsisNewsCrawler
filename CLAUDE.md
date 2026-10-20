# Opsis News Crawler — project guide for Claude Code

A Windows desktop news/market ticker bar (.NET 10, WinForms). It runs standalone
(`OpsisNewsCrawler.exe`) and as a plugin hosted by **Opsis / Opsis**.
Owner's language is Greek: reply to the user in Greek unless asked otherwise; code, identifiers and
comments stay in English; UI strings are bilingual "Ελληνικά / English".

## READ FIRST
1. `docs/HANDOFF.md` — current status, what is done / not done, decisions, next steps (start here).
2. `docs/USER-REQUIREMENTS-2026-10-13.md` — the owner's latest requirement list, verbatim (Greek).
3. `docs/DATA-SOURCES.md` — which market-data / news sources were verified and which are forbidden.

## Hard facts about the state of the code
- The solution builds with 0 errors/0 warnings on Windows (`dotnet build OpsisNewsCrawler.sln -c Release`) and the installer compiles (`Output\OpsisNewsCrawler-Setup.exe`). Since 2026-10-13 it has been run and checked on a real desktop; `docs/TASKS.md` says exactly which behaviours were verified (screenshots) and which were not.
- WinForms analyzer **WFO1000** is silenced in the Windows project (`NoWarn`), like in Opsis; `TickerCanvas` still uses `GetX()/SetX()` methods.
- Greek.isl is not shipped with Inno Setup 6.7: the installer uses only `Default.isl` and bilingual custom messages (the only place that still mixes the two languages).

## Build / run
```
Run-OpsisNewsCrawler-Build.bat      menu: 1 build+run, 2 build+installer, 3 both, 4 uninstall
dotnet build OpsisNewsCrawler.sln -c Release
```
The bat publishes the app to `bin\` (framework-dependent), copies the plugin to `bin\plugins\Opsis - Desktop Customization\` (the installer registers that folder under `HKCU\Software\Opsis\PluginPaths`),
and compiles `OpsisNewsCrawler.iss` with Inno Setup 7/6/5 (`ISCC`, found automatically). Output: `Output\OpsisNewsCrawler-Setup.exe`.

## Solution layout (`src/`)
| Project | Role |
|---|---|
| `OpsisNewsCrawler.Core` (net10.0) | models, `TickerState`, `AppConfig` (JSON in `%AppData%\OpsisNewsCrawler\config.json`), RSS parsing, other non-UI logic |
| `OpsisNewsCrawler.Windows` (net10.0-windows) | all WinForms UI + Win32 interop: `TickerHost` (owns window, config, refresh loops), `TickerWindow`, `TickerCanvas` (custom painted strip), `ArticlePreviewForm`, `SettingsForm`, `FaviconCache`, `Native` |
| `OpsisNewsCrawler.App` | standalone exe (tray icon, single instance mutex) |
| `OpsisNewsCrawler.Plugin.Abstractions` | `IOpsisNewsCrawlerPlugin` contract that Opsis implements against |
| `OpsisNewsCrawler.OpsisPlugin` | the plugin DLL; wraps the same `TickerHost` |

Key rule: the ticker **state lives in Core (`TickerState`), never in the UI**, so a host can pause/resume/
reposition/refresh without losing the scroll position.

## Conventions
- UI thread only for `TickerHost`/`TickerWindow`; network work is `async` and marshals back with `Invoke`.
- Network code must never throw into the UI: catch `HttpRequestException`/`TaskCanceledException`/`XmlException`/`IOException`.
- Do not scrape sites whose robots.txt/ToS forbid it (see `docs/DATA-SOURCES.md`). Do not add consent-bypass cookies.
- Do not present delayed data as live. Quotes are labelled delayed.
- Keep the app version in `src/*/*.csproj`, `OpsisNewsCrawler.iss`, and `OpsisNewsCrawlerPlugin.Version` in sync.
- Files in `docs/archive/` are historical reference only (an abandoned parallel implementation and old chat notes).

## v0.8.0 architecture notes (added 2026-10-13, evening)
- **Task list**: `docs/TASKS.md` is the live list of requirements (R1–R12, N1–N11, O1–O6) with verified/unverified state. Update it whenever a task changes state.
- **Language**: every UI text goes through `Core/Loc.cs` (`Loc.T("key")`), keys in `Core/Locales/el.json` + `en.json` (embedded; more languages = `locales\<code>.json` next to the exe). Never write mixed "Ελληνικά / English" strings. The locale files are generated: edit the script behind them or edit both JSON files, keep the same keys in both.
- **Drawing**: `TickerCanvas` renders into a 32-bit **alpha** bitmap and copies it with `CompositingMode.SourceCopy`. The window background is a DWM accent backdrop chosen by `Native.SetBackdrop` (state 2 gradient / 3 blur / 4 acrylic). Do NOT use `Form.Opacity` (it blurs the text) and do not use GDI `TextRenderer` on the canvas (alpha 0 = invisible dark text).
- **Bars**: `TickerHost` owns the main bar (`Window`) and an optional second bar inside the taskbar (`SecondWindow`). Each bar shows News, Stocks or Both (split in the middle: `TickerCanvas` x2 in one `TickerWindow`).
- **Quotes** (`Core/Quotes.cs`): Athens delayed files (Euronext), gold-api.com metals, FRED energy. Previous closes are cached in `%AppData%\OpsisNewsCrawler\quotes-cache.json`.
- **Settings UI**: `SettingsPanel` (shared by `SettingsForm` and the Opsis page) built from `FlatControls.cs` (ported from Opsis FlatCheckBox/FlatComboBox/FlatSlider/HoverButton). Colours come only from `Theme`; a host (Opsis) pushes its own with `Theme.SetColors`.
- **Taskbar integration**: free span measured by `TaskbarMetrics` (Win32 child windows + MSAA for the classic task list + UI Automation for the XAML taskbar). The Windows project therefore has `UseWPF=true` and global usings for `System.IO`/`System.Net.Http`.
- **STA**: `App/Program.cs` must keep `[STAThread]` (WebView2 live preview fails with RPC_E_CHANGED_MODE otherwise).
- **Breaking news / live preview**: `Core/Breaking.cs`, `TickerWindow.StartBreaking`, `LivePreviewForm` (WebView2, rectangular, opened only by the user from the pulsing button).
- **Opsis plugin**: contract `Plugin.Abstractions/PluginContract.cs` (copy the built DLL to `MotionDeskStudio\libs\` when it changes); Opsis side `src/Services/PluginHost.cs`. Hosted: `HostedByOpsis`, `LanguageFollowsHost`, colours pushed by Opsis, about texts shown in Opsis' About page.
- **Test switches**: `OpsisNewsCrawler.exe --settings --tab N`, `--test-breaking`; `Opsis.exe --page plugin:OpsisNewsCrawler`.

## Languages (2026-10-14)
Fourteen UI languages like Opsis: `Core/Locales/<code>.json` (el, en generated by a script; the other twelve are plain JSON, edit them directly, same keys as en.json, missing keys fall back to English). The installer has the same languages (`[Languages]`/`[CustomMessages]` in `OpsisNewsCrawler.iss`, 19 messages each) and passes the choice to the app through `first-run.json`.

## 0.9.0 additions
- Extra bars per monitor: `AppConfig.MonitorBars`, `TickerHost.SyncMonitorWindows`, `TickerWindow.TargetScreen` (bottom of that screen's work area, none of the primary-taskbar logic).
- Read later: `Core/ReadLater.cs`, Ctrl+click in `TickerWindow`, tab "Read later" in `SettingsPanel`.
- RTL: `Loc.IsRtl` (ar/he/fa/ur); `TickerCanvas` mirrors geometry (`ItemRect`, `DrawEntryRtl`), the settings panel sets `RightToLeft`.
- Updates: `Core/UpdateChecker.cs` (GitHub releases API), `Windows/UpdateInstaller.cs` (download + mandatory SHA-256 + start), opt-in `CheckForUpdates`.
- Test aids: `--snapshot <folder> [--config <file>]` (PNG of every settings tab), `--config <file>` for a normal start.

## 0.12.5 additions
- **Release only with `tools\release.ps1`** (publish, mirrors the plugin folder, checks that the plugin DLLs have the app version and that the plugin contract keeps the assembly version of the copy in Opsis\libs, compiles the installer, writes the .sha256). Never bump the `AssemblyVersion` of `Plugin.Abstractions` (frozen 0.8.0.0).
- Icon: `Windows/AppIconArt.cs` (drawn in code; `--make-icon <file>` writes `Assets\app.ico`); animated tray icon in `App/Program.cs`.
- Portfolio: `Core/Portfolio.cs` (positions, `PortfolioCalc`, `EcbRates`, `Technical`), `Windows/PortfolioForm.cs`; the quote service fetches portfolio instruments too (`alsoNeeded`) and keeps 130 daily closes in `QuoteStore`.
- Dependencies: `installer\dependencies.iss` (shared with the Opsis installer) and `Windows/Dependencies.cs`.
- Locales are edited DIRECTLY in `Core/Locales/*.json` now (the old generator script is gone); keep the same keys in all 14 files.
- Test aids: `--snapshot <dir> [--config f]`, `--snapshot-portfolio <png> [--config f]`, `--portfolio`, `--make-icon <ico>`.
