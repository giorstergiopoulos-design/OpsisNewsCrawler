# Changelog

The complete history (all versions, in Greek and English) is also inside the program: Settings → Information → History.
Detailed notes per release: `docs/RELEASE-*.md` and the GitHub releases.

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
