# Data sources — what was verified (2026-10-13)

All checks were done with a fetch tool from a sandbox (no raw-HTTP access to most sites), so "works" means
"the content is publicly returned", not "tested from the Windows app".

## News (RSS) — verified as valid RSS 2.0
| Site | URL | Notes |
|---|---|---|
| Ναυτεμπορική | https://www.naftemporiki.gr/feed/ | one item had an empty `<pubDate>` → parser tolerates missing dates |
| OT.gr | https://www.ot.gr/feed/ | |
| Bankingnews | https://www.bankingnews.gr/rss | pubDate not RFC822 (`2026-10-09 10:22:33 +0300`) → parser fixes `+0300` |
| Mononews | https://www.mononews.gr/feed | |
| ΕΡΤ | https://www.ertnews.gr/feed/ | |
| in.gr | https://www.in.gr/feed/ | |
| in.gr — world (Greek, international news) | https://www.in.gr/world/feed/ | verified; use as the Greek "International" default instead of BBC |
| iefimerida | https://www.iefimerida.gr/rss.xml | |
| BBC World (English) | https://feeds.bbci.co.uk/news/world/rss.xml | NOT verified (site blocked for the tool); keep as optional preset, not default |
| ECB / ATHEX announcements | athexgroup.gr publishes Atom 1.0 for company announcements | not yet used |

Not usable / not verified: capital.gr/rss (could not fetch), bankingnews `?format=feed` (returns HTML), ertnews category feed (error).
The owner wants Greek-language international news and **user-chosen sites**, see requirements.

## Market prices
| Source | Result | Decision |
|---|---|---|
| **Euronext Athens delayed trade files** `https://athens.euronext.com/en/market-data/data-services/delayed-feed` | Official, free, 15-min delayed. Listing page links `.../trade-document/PO_DAILY/Daily_PostTrade_YYYYMMDD_HH.MM.SS.csv` (newest first, 5 shown, a new cumulative-for-the-day file about every 15 min). `;`-separated, header includes `trading_date_time;isin_code;price;price_currency;...;quantity;...` (venue MIC column: `XATH` = cash equities with `GRS…` ISINs, `XADE` = derivatives with `GRV…`). **No ticker/name column — ISIN only.** Files stay available until midnight of the next business day. Page gives no explicit re-use licence; ATHEX info-services says delayed (≥15 min) data is free of exchange fees and needs no agreement. | **USE for Athens** (`AthensDelayedFileProvider`). Last price per ISIN = row with latest `trading_date_time`. Previous close: previous day's last file if listed, else local cache of yesterday's last price, else show no change. |
| Google Finance `https://www.google.com/finance/quote/{TICKER}:{EXCH}?hl=en` | Returned price + % + abs change + timestamp text for ASML:AMS, ADYEN:AMS, BNP:EPA, UCB:EBR, EDP:ELI, ENI:BIT. **Athens tickers (ETE, ALPHA, OPAP, HTO, EEE : ATH) returned "Page Not Found".** Oslo (OSL) and Dublin (ISE) could not be tested (rate limited). robots.txt has no Disallow for /finance, but Google's ToS forbids automated queries and EU users get a consent wall. | **Optional, opt-in, off by default, "unofficial / at your own risk"** for non-Athens Euronext exchanges (`GoogleFinanceQuoteProvider`). Raw HTML was never seen: parser must be tolerant (data-last-price attribute, else tag-stripped text pattern `price arrow_upward +0.94%`). Do NOT add consent-bypass cookies; if a consent page is detected, report it in the UI. |
| Yahoo Finance chart API `query1.finance.yahoo.com/v8/finance/chart/{symbol}?range=6mo&interval=1d` | Answers without a key (tested 2026-10-21: ETE.AT, GC=F; 129 daily candles). Yahoo's ToS do not allow automated use. | **Optional since 0.13.1, OFF by default, "unofficial, at your own risk"** (Sources > Markets flow > Price history, `AppConfig.HistoryFromYahoo`): only fills the stored daily history (`YahooHistory`), one request per instrument and day, 1 s apart, stops at 401/403/429, honest User-Agent, no cookies or crumbs, no limit bypass. Same status as the Google Finance option below. |
| Stooq CSV | robots.txt `Disallow: /` for everyone except two search bots, and since 2026-10 the CSV address answers with a JavaScript proof-of-work page ("verify your browser") = bot protection | **Not supported.** Passing the check would bypass bot detection. |
| live.euronext.com ajax (`getDetailedQuote`) | returns AES-encrypted JSON `{ct,iv,s}` | **Do not reverse-engineer** |
| athens.euronext.com `instruments/stocks` table | table is filled by JavaScript; endpoint unknown (needs browser DevTools network tab) | open question — if the XHR/JSON endpoint is found it would be the best Athens source (symbol, issuer, ISIN, last, %, time) |
| Python sample from the owner (Tkinter + Google Finance scraping) | URL in the sample is broken (`https://google.com{ticker}:ATH` — no `/finance/quote/`), relies on obfuscated CSS classes `YMlKec fxKbKc` / `Jw7C9b`, and Athens tickers are not found on Google Finance | not ported as-is |

## Athens ISINs (memory-recalled; all pass the ISIN check digit, **must be verified against live data**)
OTE GRS260333000 and Jumbo GRS282183003 were both seen in the real 2026-10-12 trade file (confirmed to exist).
Recalled, unverified: NBG GRS003003035, Alpha GRS015003007, Eurobank GRS323003012, Piraeus GRS014003032,
OPAP GRS419003009, PPC GRS434003000, Hellenic Energy GRS298343005, Motor Oil GRS426003000, Aegean GRS495003006,
Euronext Athens/ATHEX GRS395363005. The 2026-10-12 file also showed very active ISINs `GRS829003003`, `GRS830003000`,
`GRS831003009` (probably banks) that do NOT match the recalled bank ISINs — so ISINs may have changed or the recollection is stale.
=> The settings UI must let the user add `SYMBOL;ISIN;Name` lines, and the app should show "—" (never a wrong price) if an ISIN is absent from the file.

## Google Finance exchange codes seen working
AMS (Amsterdam), EPA (Paris), EBR (Brussels), ELI (Lisbon), BIT (Milan). Likely: ISE (Dublin), OSL (Oslo) — untested. Athens = ATH (not found).

## Added 2026-10-13 (verified from this Windows PC)
| Source | Use | Notes |
|---|---|---|
| Euronext Athens delayed trade file (`.../trade-document/PO_DAILY/Daily_PostTrade_YYYYMMDD_HH.MM.SS.csv`) | Athens quotes | Downloaded live: ~7 MB, 59 892 rows, cumulative for the day; listing shows only today's 5 files, so the previous close is cached locally. All 13 default ISINs were checked against the file and OpenFIGI (OPAP is now Allwyn AG `GRS419003009`; the banks are `GRS829003003` Eurobank, `GRS830003000` Alpha, `GRS831003009` Piraeus, `GRS533003000` Optima). |
| OpenFIGI mapping API (`api.openfigi.com/v3/mapping`) | development only: identify an ISIN | not used by the app |
| gold-api.com `GET /price/{XAU,XAG,XPT,XPD,HG}` | metals | "free to use 24/7, no rate limiting for real time prices" (site); no history without an API key, so changes come from the local cache |
| FRED public CSV `fred.stlouisfed.org/graph/fredgraph.csv?id=DCOILBRENTEU / DCOILWTICO / DHHNGSP` | oil and natural gas | U.S. EIA daily data, several days behind; shown as "daily price dd/MM" |
| `FeedDiscovery` | RSS auto-discovery | verified: `in.gr` → `https://in.gr/feed`; `bbc.com/news` → nothing found |

## athens.euronext.com ticker tape (checked 2026-10-14)
`/en/fragments/tickerTapeData?type=2` returns ~60 Athens shares (name, price, open, close, change) as JSON over plain HTTP; robots.txt does not disallow it. It is an internal site fragment (no published API/terms), so it is NOT used; the app keeps the official delayed trade file + OpenFIGI. Candidate fallback only.

## Weather (added 2026-10-14)
Open-Meteo (https://open-meteo.com, `api.open-meteo.com/v1/forecast`, `current=` variables): free for non-commercial use, no API key, data licensed CC BY 4.0 — the attribution is in the Weather tab note and here. One request carries up to 100 places (the 71 Greek cities take about half a second). Readings are cached 10 minutes, refreshed every 15. Terms: https://open-meteo.com/en/terms
Other keyless prices added the same day: gold-api.com (BTC, ETH), Frankfurter/ECB (currency pairs; robots.txt allows everything).


## Pyth Network Hermes (added 2026-10-15, not yet verified live)
`GET https://hermes.pyth.network/v2/updates/price/latest?parsed=true&encoding=hex&ignore_invalid_price_ids=true&ids[]=<id>...` — public, no key, one request for all ids. Used for Brent (Commodities.Index.BRENT/USD), WTI (WTI1M, 23/5), natural gas (NATGAS, 24/7). Prices are `price * 10^expo`; entries with a confidence interval above 2 % are dropped. If the answer is older than 30 minutes the bar shows its date; FRED/EIA (daily, about a week late) is the fallback. Terms of use of the service were not reviewed in depth: review before any commercial distribution.


## Update frequency of once-a-day sources (2026-10-17)
- ECB reference rates (Frankfurter): published once per working day (~16:00 CET); polling more often returns the same numbers. No verified free, ToS-clean live FX feed was found; Pyth FX feed ids were not verified, so they are not used.
- Euronext other markets: end-of-day files. EIA/FRED: daily, ~a week late (only a fallback now; Pyth is first).
- yfinance: a Python library that scrapes Yahoo Finance (Yahoo ToS forbids it); it cannot be a .NET dependency and is not used.

## Added 2026-10-20
- News feeds checked live (HTTP 200, valid RSS, items parsed by the real provider): Defence Point `defence-point.gr/news/feed/`, Defence Review `defencereview.gr/feed/`, Militaire.gr `militaire.gr/feed/`, Breaking Defense `breakingdefense.com/feed/`, Defense News (arc outbound feed), POLITICO Europe `politico.eu/feed/`, Gazzetta `gazzetta.gr/rss`, in.gr Sports `in.gr/sports/feed/`, BBC Sport, ESPN. Not usable: sport24.gr (403), Kathimerini diplomacy (410), pentapostagma.gr (404).
- Pyth Hermes `/v2/updates/price/latest` returns 401 (API key needed) as of 2026-10-20; the program stops asking for 12 h and uses FRED/EIA.
- Ideas (no code copied): stonks-cli (MIT) — portfolio/P&L dashboard; greek-stocks-ai (MIT badge, no licence file) — RSI and SMA crosses. Microsoft download links used by the dependency installer: `aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe`, `go.microsoft.com/fwlink/p/?LinkId=2124703` (WebView2 bootstrapper); run only with a valid Microsoft Authenticode signature.
