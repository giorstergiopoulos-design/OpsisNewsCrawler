# Changelog

The complete history (all versions, in Greek and English) is also inside the program: Settings → Information → History.
Detailed notes per release: `docs/RELEASE-*.md` and the GitHub releases.

## 0.14.3 — 2026-10-23
- The icon is rectangular: the breaking-news intro draws the whole artwork (four news symbols) over the full bar height, crisp at 100/150/200 % DPI (rendered at the final pixel size, only scaled down while it grows); the intro echo ring is a rounded rectangle.
- Settings header icon is the same wide icon. Letter badges and favicon plates are rounded squares, not circles; favicons are kept at 64 px and scaled down; the icon next to a headline grows with the bar (20 px at 42 px).
- New --snapshot-bar <png> switch; review with proposals in docs/REVIEW-2026-10.md. Tests: 113.

## 0.14.2 — 2026-10-22
- Severe-weather alert: clicking it opens the warnings page (meteo.gr) and the bar returns to normal news. Before, nothing opened and the bar stayed red and static for 30 minutes.
- While only alerts without an article (weather, price limits) are active, the normal news keeps scrolling after them so the bar never looks frozen. Clicking a price-limit alert dismisses it.

## 0.14.1 — 2026-10-22
- Breaking news: clicking an urgent headline opens it and the bar goes straight back to normal news (before, it stayed red and showed only the other urgent items until the time ran out).
- New breaking-news sound: a newsroom-style stinger (whoosh, three bell hits and a ringing chord, ~2.3 s) instead of a plain beep.
- Default bar position: inside the free part of the taskbar, between the weather and the date.
- The desktop bar has the same glass/transparency as the Opsis DeskStrip, and rounded corners are drawn by Windows (no jagged edges).

## 0.14.0 — 2026-10-22
- **Low-power mode inside Opsis**: when the host reports the Low hardware tier, the bar scrolls at ~30 fps instead of ~66 and polls the taskbar every ~90 ms instead of 30 ms. The scroll is time based, so the visible speed does not change.
- The plugin contract gained the optional `IPluginProfileAware` (the source in this repo was missing it although the Opsis host already shipped it).

## 0.13.8 — 2026-10-21
- **MACD**: a panel under the chart (lines and histogram) and a third back-test strategy (MACD crossover).
- **News mood** page in the market analysis: simple Greek/English word lists on the loaded headlines (negation aware), overall mood, per watched share and a list of the strongest headlines. A rough guide, not investment advice. 14 languages.
- **The animated program icon** is shown in the header of the settings (own window and the page inside Opsis) and the title-bar icon of the settings window moves too, under the same condition as the tray icon.
- **Updates follow the logic of GearWin's self-update**: a card with the current version, "Check now", "Download & install", "What's new" and a progress bar; a check at every start and every 12 hours (on by default, also for existing settings once; waits for the network), a tray notification once per version, a confirmation before anything is installed, only this repository's GitHub releases (https, exact path, exact file name), the SHA-256 that GitHub publishes for the setup (fallback: the .sha256 file; without either nothing runs automatically and the release page opens), a mismatch deletes the file, the hash is checked again right before the setup starts, the setup runs silently after the program has ended and the program opens again.
- 8 new unit tests (85).

## 0.13.5 — 2026-10-21
- **Bar styles are now visible on the full-width bars**: a rounded or pill main bar (and the second and monitor bars) floats with a 28 px margin and a thin outline instead of reaching both screen edges, where the curves were invisible.
- **Yahoo Finance catalogue** (opt-in with the same switch as the price history, off by default, unofficial): search any share, index, currency, commodity, ETF or crypto that Yahoo offers (one request per search, only when you press Search) or add ready-made groups (indices incl. the Athens General Index, commodities, currencies, crypto); they show on the bars like the other prices. Safety limits: at most 30 instruments, at most one request per instrument every 15 minutes (a cached answer in between), requests one after the other with a pause, a 6-hour stop at the first 401/403/429, nothing is bypassed.
- The Yahoo settings text no longer mentions other services.
- StoreKey is no longer written to the settings file.

## 0.13.2 — 2026-10-21
- Bar styles (rectangle, rounded, pill) now really apply: the window shape was not set when the window was created later; the main, second and monitor bars have a style too (Bars → Placement). Desktop bar style as before.
- Settings → Bars: the monitors card sits right under the main bar.
- Energy prices work again: FRED now serves a bot-check page, so the EIA public history pages are the fallback.
- Everything that was only in Greek/English is now translated into all 14 languages: ~100 UI texts (settings sections, weather conditions, article menu, alerts, feed-test messages), Help, Instructions and the newest version history (older history entries and the MIT licence text stay English).
- The "Monitors" settings are now the last section of the Bars page, after the desktop bar.
- QA pass over the settings: every control of the panel was driven in-process in all 14 languages (no raw keys, no exceptions), buttons clicked, Apply saved and reloaded.

## 0.13.1 — 2026-10-21
- All new texts of 0.12.8–0.13.0 translated into the 12 other languages (analysis window, back-test, fear & greed, assistant, bar styles, tray, weather details, network messages). Help/Instructions/History/License remain English in those 12 languages.
- Optional price history from Yahoo Finance (off by default, unofficial, at the user's own risk): charts, back-test and gauge work from the first day. Stooq is not supported (bot check).
- Bollinger bands on the candlestick chart; Euronext shares of other markets and the portfolio now find their stored prices (store key MIC:ISIN).

## 0.13.0 — 2026-10-21
- **Market analysis window**: candlestick chart (SMA20/50, hover figures; exact open/high/low/close for Athens shares from the trade file), back-test (SMA crossover, RSI), fear & greed gauge computed from the stored prices, and an optional assistant using the user's own Anthropic key (encrypted for the Windows account, not exported, nothing sent until Send).
- **Settings window**: one left navigation rail instead of two rows of buttons; the Bars page now holds every bar setting in clear sections (placement, what each bar shows, desktop bar).
- Desktop bar styles (rectangle, rounded, pill); the bar preview is now a miniature screen plus readable close-ups of every bar.
- Tray icon: one Pause/Resume item; the Opsis tray menu gets Close and Open for the crawler.
- Prices are stored as daily candles (`quotes-cache-ohlc.json`); 8 new unit tests (64 in all).

## 0.12.8 — 2026-10-20
- While an auto-hiding taskbar (and the bar inside it) is hidden, the desktop bar appears at the place and size the user chose (option in Bars, on by default, 14 languages). Tested with the Windows auto-hide switched on and off again.
- MotionDesk → Opsis rename finished in the plugin folder: the installer removes old `OpsisNewsCrawler.MotionDeskPlugin.*` files and the release script refuses any MotionDesk-named file in the payload.
- Opsis (1.9.8) adopted `installer\dependencies.iss` and added a plugin Uninstall action; no change needed here.

## 0.12.5 — 2026-10-20
- **Merged** the parallel 0.11.5.3 line (alert sound, severe-weather warnings, weather map, desktop bar, OPML, settings export/import,
  tray snooze, 3-tab settings) with 0.8–0.10. Build 0/0, 56 unit tests.
- New application icon + animated notification-area icon (news, sports, defence & diplomacy, economy passing right to left).
- New news categories: Defence & Diplomacy, Sports (feeds verified live).
- Portfolio window (stonks-cli idea): value, profit/loss in euro, today, RSI, SMA20/50 signal.
- Dependencies page in the installer (`installer\dependencies.iss`) and in Information; `/DEPS=` for silent installs.
- Position tab merged into Bars; all cards the same width.
- Weather/map window at the far right edge, touching the top of the bar or taskbar.
- Embedded bar slides with an auto-hiding taskbar.
- Plugin renamed `OpsisNewsCrawler.OpsisPlugin.dll`; plugin contract assembly version frozen at 0.8.0.0; `tools\release.ps1` checks both.
- Pyth Hermes needs an API key now (HTTP 401): paused for 12 h, daily EIA prices used.
- While an auto-hiding taskbar is hidden (and the bar inside it with it) the desktop bar appears at the place and size the user chose (option in Bars; on by default).

## 0.10.2 — 2026-10-15
- Update inside Opsis: download, SHA-256 check, silent install after Opsis closes. Log of the embedded bar's geometry.

## 0.10.1 / 0.10.0 — 2026-10-14
- Weather for 71 Greek cities (bars and a small desktop bar), scrolling settings tabs, equal effects for the weather bar.

## 0.9.x — 2026-10-14
- A bar per monitor, "Read later" (Ctrl+click), right-to-left layout, optional update check, select-all for shares, independent price sources
  with retry and log, bars on/off, crypto and currencies.

## 0.8.x — 2026-10-13/14
- 14 languages (app and installer), Euronext catalogue (all shares of an exchange), memory/security fixes, bar preview in settings.
