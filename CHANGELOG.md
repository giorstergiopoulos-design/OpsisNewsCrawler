# Changelog

The complete history (all versions, in Greek and English) is also inside the program: Settings → Information → History.
Detailed notes per release: `docs/releases/RELEASE-<version>.md` and the GitHub releases.

## 0.16.2 — 2026-10-29
- **Security audit** (report in `docs/SECURITY.md`, trust model and open items included). Fixed: downloads of updates and of family setups now stop unless the *final* address after redirects is GitHub or its CDN; the checksum file is read with a 4 KB limit; the update setup is saved as `.part` and renamed only after the SHA-256 matches; the WebView2 bootstrapper must be signed by exactly "Microsoft Corporation" (path passed in an environment variable, temp folder removed afterwards); asset names must be plain file names; "Open" in the family window starts only an existing .exe; the GDACS feed is parsed without DTDs (entity-expansion attack) and geocoding / feed discovery answers have size caps; `crawler.log` no longer contains the Windows account name. No vulnerable NuGet packages.
- Open: no code-signing certificate, plugin DLL integrity is the host's job (Opsis), the update trust root is GitHub itself.
- Translations of the history entry in all 14 languages. Tests: 353.

## 0.16.1 — 2026-10-29
- **Disasters flow**: natural disasters have their own page in the Sources group of the settings (it was a card of the weather page in 0.16.0).
- **Speed per bar**: the main bar, its second half (split bar), the second bar, the desktop bar and the bars of the other monitors each have their own speed (0 = like the main bar). The mouse wheel changes only the bar, or the half of a split bar, it is turned over. New card "Speed of each bar" on the General page.
- **Yahoo catalogue**: the Markets page lists the whole built-in catalogue to pick from (more than 100 instruments in 7 groups: indices, commodities, currencies, crypto, Athens shares, US shares, ETFs; every symbol checked against the chart service on 2026-10-29), with a group filter and instant filtering by typing; Search still asks Yahoo for anything else.
- **Universal sizes**: all settings cards keep the same width and the content adapts inside them; labels of setting rows wrap instead of being cut off in long translations; all cards of the Opsis family window have the same height. A layout audit (`--audit-layout <report>`) checks every page in every language: it found the Euronext shares card (up to 846 px in German) and the weather card overflowing; both fixed; the audit reports nothing for all 14 languages.
- Translations of every new text in all 14 languages. Tests: 326.

## 0.16.0 — 2026-10-27
- **Natural disasters** (new bar content "Natural disasters"): earthquakes from USGS and EMSC, and events from GDACS (UN/EU) and NASA EONET (fires, floods, storms, volcanoes). The same earthquake reported by several organisations is one item with the average magnitude and the range (for example M5.7, range 5.6-5.8, 3 sources). Filters: region (Greece and neighbours, Europe, world), smallest magnitude, maximum age, kinds, sources, refresh interval (5-240 min, default 15). Each feed is fetched on its own, so one that fails only adds a notice. Tests: parsers, merging, filter, text.
- **Any place** in the weather list: search by name (Open-Meteo geocoding, any country), by Greek postcode (OpenStreetMap Nominatim) or by coordinates (37.78, 20.89 / 37.78N 20.89E). Added places are marked with a star and stay in the list (at most 60). Nominatim is used only when Search is pressed, at most once a second, with an identifying User-Agent. Postcodes name an area, not one place; the name or coordinates identify a place better. The Canadian/US postcode formats are not handled.
- **Animated icon** shows the current weather of the first weather city and the highest active alert (triangle colour: yellow, orange, red). The tray tooltip gives the same in words. The settings header icon always moves (the tray option only controls the tray icon).
- **Mouse wheel** over the bar changes its speed; the welcome guide now says so (14 languages).
- **Help**: the Help and Instructions pages have buttons for the welcome guide and the Opsis family window.
- **Raw materials**: Brent, WTI and natural gas use the Yahoo Finance futures (about 15 minutes delayed, one request per instrument at most every 15 minutes) instead of the days-old FRED/EIA values, but only when the Yahoo option is switched on (it is unofficial and off by default; Pyth Hermes needs a key since 2026-10). Without it the daily values stay, with their date.
- Translations: every new text (disasters, places, weather icon, guide line) is in all 14 languages; the test that fails on English left in other languages passes.
- Not done in this version (stated, not hidden): speed set per bar and per half of a split bar; a full search over the whole Yahoo catalogue in the settings; live verification of the disaster feeds on the owner's PC (only parsers and the feeds' current answers were checked).
- Tests: 308.

## 0.15.0 — 2026-10-27
- **The crawler themes are independent of Opsis**: inside Opsis you can now choose any crawler theme (Follow Windows, Light, Dark, Midnight, Forest, Dawn, Contrast, with or without the Windows accent). A new choice "Follow Opsis" uses the Opsis colours; existing installs are switched to it once, so nothing changes until you pick another theme.
- **Welcome guide**: six short pages with the animated icon, shown on the first start of a new major/minor version (not inside Opsis, which has its own), from the tray menu "Welcome guide", with "Don't show again". Closing it (Finish, Skip, Esc) shrinks and fades it into the header icon of the settings window when that is open, otherwise into the tray area (a plain fade when animations are off or the PC is Low-tier).
- **The Opsis family** window (tray menu): one card per app of the shared catalogue (installed state read from the registry/known paths, "Update available" from GitHub, Open, Install / Download, Details). A download needs your confirmation (file, size, source), comes only from the app's own GitHub releases, is checked against the SHA-256 (GitHub digest or the .sha256 file) and deleted on a mismatch, and is never started by the program. Catalogue: `suite/apps.json` of the Opsis repo, fetched at most once a day (5 s timeout, https and GitHub hosts only, 256 KB), with a copy built into the program.
- **Update notice**: first check 30 s after the start, then every 24 h, ETag so an unchanged answer is free, once per version, not while a full-screen app is in front, and not inside Opsis (Opsis announces).
- Inside Opsis the plugin only adds the two menu entries; nothing is shown by itself.
- Fixed: in 0.14.7 twelve language files contained an unescaped quotation mark in the history text and could not be read (menus showed raw keys); a new test checks that every language file is valid and has the same keys and placeholders.
- 14 languages for all new texts.
- **Animated icon** (settings header, welcome guide, family window, tray, title bar) now passes six symbols: newspaper, ball, shield, economy, **weather** and **natural-disaster alert**; the program icon (`app.ico`) shows the same six.
- **Bug audit** (`--audit-settings <report>` changes every check box, drop-down, number box and slider of the settings and reads the result back): 59 controls checked, 51 reach a setting and are shown again after reloading, 4 are input fields of "add" forms (nothing to save), 4 were not reachable. Found and fixed by reading the controls: **no keyboard operation** - the flat buttons could not be reached with Tab, the drop-down list ignored Up/Down/Home/End/F4/Space/Enter, the slider ignored the arrow/Page/Home/End keys, and none of the four showed where the keyboard focus was. Now all four are keyboard-operable with a visible focus ring (checked by the audit's keyboard section: 11 of 11). **Still open / not verified**: layout at 150 % and 200 % system scaling (the bar itself is checked at those heights, the settings window is not), Arabic layout of every settings page, and the settings page hosted inside Opsis.
- Installer: wizard pictures made from the app artwork at 100/125/150/200 % (`--make-brand-art`); the release script also builds a portable zip, both with `.sha256`, and collects everything in `release\<version>-assets`.
- Repo: README rewritten (icon, current features, new screenshots in dark and light, SHA-256 verification steps, requirements, third-party notices, the Opsis family), social-preview image (`docs/img/social-preview.png`); removed the abandoned `docs/archive`, the old per-release notes of 0.11-0.14, the zip hand-off note and the outdated screenshots; the CI version check no longer hard-codes 0.11.5.3 (it was failing on every push) and now checks that all versions, the installer, the About text and the changelog agree.
- Translations: the history entries 0.14.3 to 0.15.0 and the "Windows accent colour" option, which had been left in English in twelve languages, are now translated in all 14; a new test fails when any text of the 14 language files is still English (except the product name, the MIT licence text and technical names). The translations were written by the model, not by native speakers, and deserve a native read-through (all twelve non-Greek/English languages).
- Tests: 245.

## 0.14.7 — 2026-10-23
- The desktop bar follows the option Keep it above other windows in every case (0.14.5 forced it on top while standing in for a hidden taskbar).

## 0.14.6 — 2026-10-23
- Auto-hide taskbar: detected correctly on Windows 11 (the taskbar is slid down to 1-2 pixel rows, not cloaked), so the bar inside it hides and the desktop bar takes over. Verified live with the Windows auto-hide on.

## 0.14.5 — 2026-10-23
- Standalone theme choices match Opsis: Midnight, Forest, Dawn, Contrast and an option for the Windows accent colour. Inside Opsis the theme controls are hidden (Opsis pushes its theme).
- Smoother animated icon (header 25 fps, tray 60 ms, 64-96 frames per loop).
- The desktop bar can stand in for a hidden auto-hide taskbar; `--test-taskbar-hidden` simulates it; runs with their own `--config` no longer collide with the running instance. Tests: 128.

## 0.14.4 — 2026-10-23
- The same story from different publishers (different wording) is shown once (word-set overlap, accents ignored).
- Feeds use ETag / If-Modified-Since; per-feed health (last success, failures in a row) is tracked in the provider.
- After resume from sleep, news, quotes and weather refresh after 6 s.
- Review corrected: exact-title de-duplication already existed. Tests: 119.

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
