# TASKS — Opsis News Crawler + Opsis plugin integration

Maintained by Claude Code: updated every time a task changes state. Last update: 2026-10-13 (evening).
Legend: ✅ done & verified · 🟡 code done, NOT fully verified on a real desktop · 🔄 in progress · ⬜ todo · ⛔ blocked

Sources: `docs/HANDOFF.md` (R1–R12), `docs/USER-REQUIREMENTS-2026-10-13b.md` (N1–N9, verbatim Greek, from Desktop\newscrawler.MD and chat).

## Requirements from HANDOFF (R)
| ID | Task | State | Notes |
|---|---|---|---|
| R1 | Quotes: Euronext Athens (+ other exchanges) | 🟡 | Athens delayed files verified live (11 stocks, change vs previous close cached or "since open"); other Euronext exchanges NOT done |
| R2 | Follow auto-hide taskbar | 🟡 | `TickerWindow.FollowTaskbarTick`; auto-hide is off on this PC, untested |
| R3 | AppBar: maximized windows stop at the bar | ✅ | verified: work area 1400→1358 px while running, restored on exit |
| R4 | Custom position: X/Y/width + move with mouse | 🟡 | boxes render OK; mouse drag untested |
| R5 | Sources per category, own categories, RSS auto-discovery | 🟡 | UI renders; discovery verified live (in.gr → feed found; bbc.com/news → none) |
| R6 | Light/dark + transparency | ✅ | dark + light verified (acrylic, readable dark text on light) |
| R7 | Dark/mono favicons made visible | 🟡 | `FaviconCache.TryGetFor`; untested visually |
| R8 | Crawler windows look like Opsis | ✅ | flat Opsis-style controls (see N2) |
| R9 | Read articles disappear | 🟡 | `ReadStore`; untested by clicking |
| R10 | Bar integrated in the taskbar, same colours | ✅ | verified: sits in the free space (MSAA measured 19 buttons), clear backdrop |
| R11 | Hide when another app is fullscreen | ✅ | verified: strip visible → hidden during a fullscreen window → visible again |
| R12 | Plugin: no own tray icon, items in Opsis tray menu | 🟡 | contract + Opsis menu code written; menu not clicked |
| — | Settings: Help / Instructions / History / License | ✅ | tabs (standalone) · moved to Opsis About page when hosted (N7) |
| — | Plugin folder `plugins\Opsis - Desktop Customization\` | ✅ | |

## New requirements (N)
| ID | Task | State |
|---|---|---|
| N1 | Taskbar-integrated bar fully transparent: only text + logos | ✅ verified (blur-behind with empty tint = identical to the taskbar) |
| N2 | Settings look exactly like Opsis (tabs, buttons, dropdowns, ticks, sliders, scroll bars) | ✅ verified by screenshot (General, Bars, Markets); other tabs rendered earlier; light theme of the panel untested |
| N3 | Two bars (above taskbar + inside taskbar), each news or stocks; upper bar can be split in the middle | ✅ verified by screenshot (split main bar + stocks bar in the taskbar) |
| N4 | Breaking news: red bar, capitals, intro (icon grows, then "ΕΚΤΑΚΤΗ ΕΠΙΚΑΙΡΟΤΗΤΑ"), pulsing live button, rectangular live preview with play/pause + maximize/restore | ✅ verified by screenshots: red bar + intro text + pulsing button; preview opens as a rectangle and loads a page in WebView2; play/pause and maximize buttons not clicked |
| N5 | Transparency copied from Opsis (background only, text stays crisp) | ✅ verified dark + light; the slider changes the background only |
| N6 | Optional edge fade | ✅ verified (both ends fade) |
| N7 | Properties/help/instructions/history/license in Opsis "About" as a "Plugins" section | ✅ verified in Opsis (section + 4 buttons); text dialogs not opened |
| N8 | Stocks bar also shows metals, oil, other commodities | ✅ shown in the bar (gold, silver, copper via gold-api.com; Brent, WTI, gas via FRED daily); no change % for metals until a second day is cached |
| N9 | Single-language UI, locale files for more languages | ✅ Greek and English verified by screenshot (141 keys, same keys in both); hosted language push built, not switched live inside Opsis |
| N10 | Keep the light blue; user-chosen colour; bar follows theme and Windows colours | ✅ verified: light blue main bar, accent / custom modes implemented (custom colour dialog not clicked) |
| N11 | User can choose the backdrop preset (accent state 2 / 3 / 4) per bar | ✅ verified: acrylic (main bar) and gradient (taskbar bar) on screen |
| N12 | Settings window can have the same transparency as Opsis (60–100 %, own slider; inside Opsis it follows Opsis' window opacity) | ✅ verified at 90 % on screen |
| N13 | No stray lines on buttons / check boxes (crawler) and equal border thickness on the collapsed sidebar buttons (Opsis) | ✅ cause: antialiased background fill leaving edge pixels, and a 42 px tile in a 42 px button; both fixed and checked in zoomed screenshots |

## Opsis integration (O)
| ID | Task | State | Notes |
|---|---|---|---|
| O1 | Plugin loader (`PluginHost`), contract DLL in `libs\` | ✅ | verified |
| O2 | Sidebar page = shared `SettingsPanel` | ✅ | verified with the new flat panel in Opsis (Opsis colours, language row hidden, no help tabs) |
| O3 | Plugin items in Opsis tray menu | 🟡 | |
| O4 | No conflicts with DeskZones / widgets / animated wallpaper | 🟡 | NoSnap window property, AppBar work-area, DeskStrip reposition, fullscreen own-process rule; not exercised |
| O5 | Installer: detects Opsis, asks integrated (plugin into %AppData%Opsisplugins) or separate install | ✅ verified: wizard shows (Greek, Opsis 1.9.3 found), silent integrated + separate installs and uninstalls tested with a copy of the script; the real Opsis plugin folder was not touched |
| O7 | Installer error 0x80070003 ("/" in the uninstall shortcut name) | ✅ fixed; shortcuts created and removed correctly in the test |
| O6 | Opsis `PROJECT_STATE.md` entry | ✅ | |
| O8 | The Opsis installer does NOT carry the crawler; it offers the task "download and install together" (downloads the crawler setup and installs it integrated; Opsis installs anyway if the download fails) | ✅ verified with test copies and a local web server (success and unreachable). The release URL is an assumption. Both real installers rebuilt 2026-10-14 (Opsis 68.7 MB, crawler 2.7 MB), not run on the real system |
| O9 | Opsis Settings: "Load plugins" switch (`AppSettings.PluginsEnabled`, applies after restart) | 🟡 built, not clicked |
| O10 | Opsis command palette lists the plugin pages (Ctrl+K) | 🟡 built; a synthetic Ctrl+K went to another window, so it was not seen working |

## Checks log
### Round 1 (before usage-limit reset)
- Build crawler 0 errors; Opsis built into a scratch folder 0 errors.
- Standalone: strip in the taskbar free space with taskbar colours; Opsis loaded the plugin (sidebar item + page + strip).
### Round 2
- Found and fixed: out-of-bounds read in the edge-fade code (crash), light theme dark-text-invisible (acrylic), host colours overridden by the crawler's own theme setting, accent state 2 showing the system accent colour (replaced by blur-behind).
- Verified by screenshots: flat settings (General, Bars, Markets), light + dark bar, split bar, second bar in the taskbar, About page "Plugins" section in Opsis.
- Live data test: 17 quotes in 1.6 s (Athens 11, metals 3, energy 3).
- NOT verified: auto-hide taskbar, AppBar reserve, fullscreen hide, read-list click, mouse move of the custom bar, favicon contrast, Opsis tray items, DeskZones/widget/wallpaper behaviour, language switch inside Opsis.

### Round 3
- Verified: AppBar work area, fullscreen hide, breaking-news intro + live button, live preview (rectangle) with WebView2 (needed [STAThread]), light-blue tint, gradient/blur/acrylic presets, English UI, new Look and Breaking tabs, Opsis-hosted panel.
- Installer compiled: Output\OpsisNewsCrawler-Setup.exe (not run).
- Still not verified: auto-hide taskbar follow (R2), mouse placement drag (R4), read-list click (R9), tray items in Opsis (R12), DeskZones/widgets/wallpaper conflicts (O4), the play/pause and maximize buttons of the preview, custom colour dialog.

### Round 4 (installer)
- The reported setup error came from the shortcut name "Απεγκατάσταση / Uninstall ..." (a / is a folder separator in Windows). Names are now single-language without it; the wizard is Greek on a Greek Windows (Greek.isl exists in this Inno Setup) and English otherwise.
- New wizard page: Opsis found (registry uninstall entry / default folders) -> propose integration; otherwise propose a separate install. Integrated = only the plugin folder into %AppData%OpsispluginsOpsis - Desktop Customization (no exe, tray icon, shortcuts); separate = the program (without the plugin copy).
- OutputOpsisNewsCrawler-Setup-v2.exe is the fixed installer; OutputOpsisNewsCrawler-Setup.exe is the OLD broken one (locked by the failed setup window still open on the owner PC).

### Round 5 (integration into Opsis)
- Plugin payload bundled into Opsis' build/publish and installer; update script + build-all step; Opsis setting and palette entries added.
- Crawler installer: `Output\OpsisNewsCrawler-Setup.exe` is now the fixed one (the old broken file was replaced once the failed setup window was closed).
- Both repos have uncommitted changes.

## Round 6 — N14 Euronext full catalogue (2026-10-14)
- ✅ Markets tab: exchange combo (Athens, Paris, Amsterdam, Brussels, Lisbon, Milan, Oslo, Euronext Growth Milan), search box, checklist of ALL shares of the chosen exchange, "Refresh list" button. Ticked shares go to the Watchlist (with Market = MIC).
- ✅ Verified headless: Athens catalogue 119 shares (delayed-trade file + OpenFIGI names), Paris catalogue downloads and parses.
- ⏳ Not yet verified: clicks in the new tab (a synthetic click missed the window), quotes of non-Athens markets on the bar, Opsis-hosted view.

## Round 7 — same 14 languages as Opsis (2026-10-14)
- ✅ App: embedded `Locales\<code>.json` for ar, de, es, fr, hi, it, ja, ko, pt, ru, tr, zh (+ el, en). All short UI texts translated; the four long texts (help, instructions, history, license) fall back to English for the new languages. `Loc.Available()` lists all of them; Opsis' culture codes (`de-DE`) are matched by the part before the dash. Arabic is not mirrored right-to-left (not done).
- ✅ Installer: the same 14 wizard languages as Opsis (`installer\Chinese.isl`, `installer\Hindi.isl` copied from the Opsis repo), all 19 custom messages translated; the wizard follows the Windows language, English is the fallback. The installer writes the chosen language into `first-run.json` and the app starts in it (verified with test copies: German and Japanese).
- ⏳ Not verified by eye: the translated settings window and wizard pages (only checked that the strings load); translation quality was not reviewed by native speakers.

## Round 8 — memory, bug and security review (2026-10-14)
Measured: the running app, 5 minutes after start — working set steady at ~103 MB (private ~38 MB), GDI objects 51→62 and then flat, handles falling; no growth. Code review of all GDI objects, timers, event subscriptions (all paired), HttpClients, WebView2.
Fixed:
- ✅ Article preview picture: a 4 MB JPEG could decode to hundreds of MB (and 24 of them were cached): now refused above 24 MP and cached downscaled to 800×600 (`SafeImage`).
- ✅ Site icons: same pixel limit; the `rel=`/`href=` regexes had lost their `\s` (icons declared as `rel = "icon"` were never found).
- ✅ HTTP clients got `MaxResponseContentBufferSize` (a server without Content-Length could stream unlimited data into memory).
- ✅ Content from feeds (item image, site icon) can no longer make the app contact localhost / private / link-local addresses (`NetGuard`).
- ✅ Live preview (WebView2): permissions (camera, microphone, location) denied, downloads cancelled, navigation only http/https, host objects and web messages off; fonts disposed; a bad live URL can no longer throw when the window opens.
- ✅ Settings, quote cache, catalogue cache and read list are written atomically (temp file + move); an unreadable `config.json` is kept as `config.json.bad` instead of being silently overwritten with defaults.
- ✅ Language code from config / host is restricted to letters (it is used in a file name).
- ✅ Market list download: unexpected network/JSON errors no longer escape an `async void` handler.
- ✅ Opsis installer: the downloaded crawler setup is only started when its SHA-256 equals the one in `OpsisNewsCrawler-Setup.exe.sha256` published next to it in the release (tested: good file installs, wrong checksum is refused and Opsis still installs).
Checked and fine: RSS XML parsing (DTD ignored, no resolver, entity limit, 5 MB cap), link opening (http/https only), installers (per-user for the crawler), Opsis plugin search paths (user folders; the elevated helper branch exits before plugins load), no secrets in the repo.
Not done / residual: the installers are not code-signed (SmartScreen warning, and the checksum only protects against a damaged or swapped file, not a hijacked release); the plugin DLL loader trusts every `*Plugin.dll` in the user's plugin folders (same trust as running a program as that user); feed URLs the user adds may point to the local network on purpose; WPF is loaded for taskbar measurement (+~40 MB working set).

## Round 9 — settings window: preview, layout, performance (2026-10-14, version 0.8.1)
- ✅ Preview of the bars (`BarPreview`) at the bottom of the Bars, Position, Look and Breaking tabs: a mock desktop with wallpaper, taskbar (start, apps, tray) and the bars drawn as the current (not yet applied) choices would show them — position (above the taskbar / top / centre / custom / inside the taskbar), content (news / prices / both split, prices left or right), second bar, colour (theme, light blue, accent, own colour, like the taskbar), transparency, background material, fading ends, light/dark theme. Refreshes 4× per second while visible only.
- ✅ Look tab: the colour/material drop-downs were cut ("…Opsis και…"); widened.
- ✅ Idle CPU of the running bar: 0.47 % of one core. Settings window builds in about 1 s.
- ✅ Test aid: `OpsisNewsCrawler.exe --snapshot <folder> [--config <file>]` saves every settings tab as PNG (no need for the window to be on screen).
- ⏳ Not checked: the preview in the Opsis-hosted page, a very small settings window, right-to-left layout of Arabic.

## Round 10 — version 0.9.0 (2026-10-14)
- ✅ Select all / Select none for the shares of the chosen exchange (Markets tab; applies to the shares listed after the search filter).
- ✅ Other monitors: Bars tab, card "Other monitors": one checkbox + content (news / prices / both) per non-primary screen (`AppConfig.MonitorBars`, `TickerHost.SyncMonitorWindows`, `TickerWindow.TargetScreen`). The extra bar sits at the bottom of that screen's work area; it follows connect/disconnect (`SystemEvents.DisplaySettingsChanged`). Smoke-tested only by creating the extra window on the single available screen (two bars, no crash). 🟡 Not tested with a real second monitor (none here).
- ✅ Read later: Ctrl+click on a news item saves it (toast "Saved for later"), list in the new "Read later" tab (open / remove / select all), file `%AppData%\OpsisNewsCrawler\read-later.json`, 300 articles at most. 🟡 Ctrl+click itself not tried on the real bar (no synthetic input while the owner works).
- ✅ Right-to-left (Arabic, also he/fa/ur codes): settings mirror (`RightToLeft`), the bar mirrors (items start at the right edge, icon at the right, text right-to-left, strip moves to the right). Checked on a screenshot of the bar and of the settings in Arabic. Not done: the article preview card and the Bars preview are not mirrored.
- ✅ Updates: General tab card "Updates": optional daily check (off by default) and "Check now" against the GitHub releases API of this repository; "Download and install" (standalone only) downloads the setup, requires and checks its SHA-256 from `OpsisNewsCrawler-Setup.exe.sha256`, then starts the normal wizard; inside Opsis only the download page is offered. The check was run against the real releases (0.8.0 sees 0.8.1, 0.8.1 is up to date). 🟡 The download-and-start path was not run (it would launch an installer).

## Round 11 — version 0.9.1 (2026-10-14)
- 🐞 Found: the 0.9.0 installer shipped the plugin payload of 0.8.1 (a robocopy with forward-slash paths failed silently), so inside Opsis the old plugin ran: no update check, old behaviour. The standalone app was 0.9.0. Fixed by `tools\release.ps1` (mirrors the plugin folder, **fails if any plugin DLL version differs from the app**, then compiles the installer and writes the .sha256). Always release with it. Verified: integrated install on a throwaway copy lands plugin 0.9.1. The update-check code itself was fine (0.8.1 → finds 0.9.0).
- ✅ Prices that never left "Loading prices…" on another PC: the slowest source (FRED, three requests one after the other, 40 s each) held back all sources and an unexpected exception made the whole run fail without a message. Now every source runs alone with its own timeout (15 s), shows up as soon as it answers, failures are logged (`%AppData%\OpsisNewsCrawler\crawler.log`, button "Open the log file" in General), the bar says "Prices could not be loaded (Athens, Energy)" instead of "Loading", and a retry follows after 60 s.
- ✅ Switch "Show the bars" (General tab and tray menu): off = every bar hidden and nothing downloaded.
- ✅ New instruments: cryptocurrencies BTC and ETH (gold-api.com, real time), currency pairs EUR/USD, EUR/GBP, EUR/CHF, USD/JPY, GBP/USD (Frankfurter = ECB reference rates, once per working day). All off until ticked in Markets.
- ℹ️ Oil and gas: FRED/EIA is the only keyless, terms-compliant source and it is published with about a week's delay (latest 06/10 on 14/10); the bar shows that date. Stooq, Yahoo, Investing etc. are not allowed (robots.txt/terms). Fresher data needs a user-supplied free API key (e.g. OilPriceAPI) — not implemented.

## Round 12 — version 0.10.0: weather (2026-10-14)
- ✅ 71 Greek cities (regional-unit capitals, plus Piraeus, Eleusis, Kropia, Hydra and well-known islands) in `Core/Weather.cs`; Open-Meteo, one request for all.
- ✅ "Weather" is a content of any bar (main, second, other monitors); each item = drawn icon (clear, partly cloudy, cloudy, fog, drizzle, rain, snow, thunder; moon at night), city, temperature with one decimal, wind speed + compass point, humidity. Compass letters and words translated in all 14 languages; city names Greek or English (other languages show English).
- ✅ Tab "Weather": search, tick cities (order of ticking = order on the bar), select all / none; preview shows the weather bar.
- ✅ Small desktop weather bar (`WeatherBarWindow`): one city, drag to move (position saved), click = next selected city, right-click = settings, optional "above other windows". Seen on screen (Santorini, 17,3 °C). 🟡 It is the crawler's own window; if Opsis has its own time/weather/city widget on the desktop, putting the crawler's weather inside it needs a change on the Opsis side (not done).
- 🟡 Not checked: weather in a hosted Opsis page, other monitors' weather bar, Arabic layout of the small bar.

## Round 13 — version 0.10.1 (2026-10-14)
- ✅ Settings tabs: ONE row that scrolls (mouse wheel over the tab row, arrows at both ends, a chosen tab is brought into view) instead of wrapping onto a second row; seen in Russian where the tabs do not fit.
- ✅ The small weather bar is now an ordinary bar window (`TickerHost.UpdateMiniWeather`): centred on the desktop by default, height 24–200 px (text and icons scale with it, `TickerWindow.SetBarHeight`), width 200–4000 px, position boxes, "Move with the mouse", "Centre on the desktop", and the SAME look as the main bar (colour, transparency, background material, edge fade). Content = the ticked weather cities; it stands still and centred when they fit, otherwise scrolls. By default it lies on the desktop below all other windows (`Native.SendToBottom`, verified in the z-order: directly above Progman); option "keep above other windows". Seen on screen at 900×64, light blue, centred, with edge fade.
- 🟡 Not checked: hosted in Opsis; the desktop layer while Opsis's animated wallpaper runs.
