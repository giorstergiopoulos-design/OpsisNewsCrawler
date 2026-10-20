# Opsis News Crawler 0.11.5.3

### Added in 0.11.5.3

- Click on a city of the weather line opens a window with the current weather and a live OpenStreetMap map (maximize / close / Esc); option in Sources → Weather flow.

---

# Included from 0.11.5.2

### Added in 0.11.5.2

- Alert sound for urgent headlines, price alerts and weather warnings.
- Severe-weather warning for the chosen city (computed from the Open-Meteo forecast; not the official bulletin).
- Price/weather alerts no longer vanish at the next news refresh.

---

# Included from 0.11.5

### Settings save and unsaved-change protection

- Apply (primary button) applies and persists the edited settings; the separate Save settings button was removed again.
- Added dirty-state tracking for the settings controls, including watchlist/feed list toggles and custom controls.
- The panel now asks whether to save, discard, or cancel when closing with unsaved changes. The prompt is used by the close button, Escape, and the standalone window close action.
- Importing settings reloads the visible controls and clears the unsaved marker; saving also clears it.
- Added localized save/unsaved-change strings for all 14 supported locales.

### Settings: three tabs, bars of any content, offline data (written without a compiler)

- Settings now have three tabs — Settings, Sources, Information — each with horizontal sub-buttons. The left rail and its search box were removed. The update frame moved to Information.
- "Desktop bar" (the former small weather bar) shows weather, markets or news; the pointer snaps onto a bar when "Move with the mouse" starts and shows the move cursor.
- The split in the middle accepts any two of: news of any category (including custom ones), markets, weather. Weather details are selectable (wind, gusts, humidity, feels-like, rain, cloud cover, pressure).
- No internet or a provider down: the last stored data stays on the bar with a message naming the problem and the time of the data.
- Breaking intro: "BREAKING NEWS · HAPPENING NOW". The "15 min delayed" label was removed from the bar and is explained in Help and in the Markets flow.
- Oil and gas near-live through Pyth Hermes (FRED fallback). Installer script: Inno Setup 7/6/5 is found automatically.

## Included in this source update

- Bumped application, Core, Windows UI, plugin, plugin contract, About dialog, and installer to `0.11.5`.
- Added `QuoteInstrument.Clone()` so the Euronext MIC (`Market`) survives watchlist editing and saving.
- Set the Athens MIC (`XATH`) on all default Athens instruments.
- Normalized the watchlist by source, market MIC, and code; duplicate entries are collapsed and remain enabled if any duplicate was enabled.
- Made the Athens provider tolerate duplicate codes instead of throwing while creating its lookup dictionary.
- Made the Euronext provider skip instruments with an empty market MIC.
- Improved RSS deduplication by canonicalizing URLs (removing common tracking parameters/fragments) and suppressing repeated normalized headlines across feeds.
- Added Core unit tests for quote cloning, watchlist normalization, RSS parsing, and OPML exchange.
- Added OPML 2.0 import/export in the Sources settings page, with XML DTD disabled, a 5 MB input limit, a 2,000-feed limit, HTTP(S)-only URLs, and duplicate filtering.
- Configured WinForms `ApplicationHighDpiMode=PerMonitorV2` and made Escape hide the settings window.
- Added an article-specific right-click menu on the ticker: open article, read later, mark as read, copy link, and open settings. Right-clicking empty space still opens settings.
- Wired “Mark as read” to the persistent read store and breaking-news cleanup, respecting the Hide Read setting.
- Added source-aware quote polling: live sources keep the configured interval, Athens delayed quotes slow to 15 minutes outside the weekday session, Euronext end-of-day data polls hourly, and daily energy/ECB data polls every 6 hours when selected alone.
- Added unit tests for quote-refresh policy behavior, disabled instruments, and mixed source selections.
- Updated the RSS User-Agent version to `0.11.5`.
- Added a Windows GitHub Actions workflow for build, tests, and release-version consistency.

- RSS quality controls in Settings → Sources: configurable maximum story age (1–720 hours; undated entries are retained), optional newest-first ordering, and an opt-in filter for news mentioning enabled Athens/Euronext watchlist shares. Round-robin feed balancing remains the default.
- RSS age/order changes, stock-news filtering, and muted-keyword changes trigger a fresh news load; both stock and muted-keyword matching are accent-insensitive for Greek headlines.
- Added a muted words/phrases control in Settings → Sources. Muted terms apply to visible headlines and breaking-news detection.
- Added localized Export Settings and Import Settings actions to the settings footer. Exports are written through a temporary file; imports validate JSON first, ask for confirmation, normalize through the existing config loader in an isolated temporary directory, apply the configuration, and save it to the active profile.
- Added temporary 15/60-minute tray snooze; it hides bars without changing the persistent Bars Enabled setting and can be cancelled early.
- Added unit tests for muted-keyword matching, word boundaries, and configuration normalization.

## Verification status

The archive was checked for version consistency, valid locale JSON, valid project XML, expected source changes, and the new article-context-menu event wiring. The development environment used to prepare this archive does not contain the .NET SDK or Windows desktop runtime, so a real `dotnet build`, xUnit run, UI interaction test, installer compilation, and live provider/API test could not be executed here. The added Windows CI workflow is intended to perform the build and unit tests on GitHub Actions.

## Remaining proposal items

This is not a claim that every item in `PROPOSALS-0.10.2.md` is implemented. Further settings redesign work (consolidated controls, source-health panels and first-run wizard), Pyth Hermes energy feed, exchange holiday calendars and configurable market hours, quote detail/sparklines and price alerts, global hotkey, PerMonitorV2 DPI validation, and WPF memory reduction remain follow-up work. The current polling policy uses conservative weekday/time heuristics, not an official exchange calendar. Live Pyth integration in particular should not be enabled until its feed IDs and response handling have been verified against real responses.



## Added after the 0.11.5 archive (unbuilt, same version number)

- **Pyth Hermes energy prices**: `Core/PythHermes.cs`; `FredEnergyProvider` now asks Pyth first (Brent, WTI, natural gas) and uses FRED/EIA only when Pyth has no recent price, showing whichever is newer (older than 30 min is labelled with its date, older than 4 days is ignored). Feed IDs come from the Hermes price list but were NOT tested against a live response: check `crawler.log` and the new status card after the first run. Energy now follows the configured refresh interval (`QuoteRefreshPolicy`).
- **Price source status** card in Settings → Markets (`QuoteService.Statuses()`): per source ok/failed, number of prices, last check, newest price time.
- New tests: `PythHermesTests`; `QuoteRefreshPolicyTests` updated for energy.
- Also in this archive (unbuilt): feed Test/Test all and ▲▼ ordering, Start with Windows, Ctrl+Alt+N hot key, price alerts, first-run settings window. See `docs/TASKS.md` round 15.
- Still not done: market holiday calendar, quote hover/sparklines, clickable quotes.
- **Settings pages merged (13 → 7: the four about pages are one "Information" page with Help/Instructions/History/License buttons)**: General + Read later, Bars + Position, Look + Breaking (the bar preview shows on pages 1 and 2), Sources, Markets, Weather. `--tab N` now uses these indexes (0–5 settings, 6 information). Unbuilt: check the merged pages scroll and look right.
- Fix: right-click article menu no longer disposes the ContextMenuStrip inside Closed (crash "Cannot access a disposed object: ContextMenuStrip" on Settings…).
