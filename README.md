# Opsis News Crawler

A scrolling news and market-price ticker bar for Windows (.NET 10, WinForms).
It runs standalone (tray icon) or as a plugin of **Opsis - Desktop Customization**.

- News from RSS feeds, with breaking-news alert and optional live preview.
- Prices: Euronext shares (Athens delayed 15 min, other markets end-of-day), metals, oil and gas.
- Bar above the taskbar and/or built into the taskbar (follows its colours, transparency and auto-hide).
- Interface in Greek and English (more languages via `locales\<code>.json`).

## Build

```
dotnet build OpsisNewsCrawler.sln -c Release
```

`Run-OpsisNewsCrawler-Build.bat` builds, runs and compiles the installer (needs Inno Setup 6).

## Notes

Developer notes are in `CLAUDE.md` and `docs/`. Data sources and their terms: `docs/DATA-SOURCES.md`.
Prices are delayed or daily and are not investment advice.

## License

MIT, see `LICENSE`.
