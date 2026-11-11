# Weather providers: comparison for the "accurate weather" request (§0Β.5)

Written 2026-11-11 for the owner to decide. The program uses **Open-Meteo** today (no key, forecast by coordinates, the geocoding search for places by name). This note compares it with the alternatives and says what was checked and what was not. The choice of a provider is the owner's: nothing was changed in the program for this note.

## What was checked (read from the providers' own pages on 2026-11-11)
| Provider | Terms read | Key | Free limit | Coverage | Licence / attribution |
|---|---|---|---|---|---|
| **Open-Meteo** (open-meteo.com/en/terms) | yes | none for non-commercial use | under 10 000 calls a day, 5 000 an hour, 600 a minute, 300 000 a month; **non-commercial use only** (paid plans for commercial use) | worldwide | CC BY 4.0 (credit required) |
| **MET Norway** (api.met.no/doc/TermsOfService) | yes | none; a real **User-Agent with a contact** is mandatory (fake strings get a permanent ban) | below 20 requests a second, cache and do not repeat identical requests | worldwide model; best resolution in the Nordic area | CC BY 4.0 (credit required) |
| **NWS / weather.gov** (weather.gov/documentation/services-web-api) | yes | none today (an API key is announced for the future); User-Agent with a contact | "generous" non-public limit | **United States only** | open data, public service |
| **OpenWeather** (openweathermap.org/price) | pricing page read | key | first 1 000 One Call calls a day free, then 0.0014 EUR per call | worldwide | its own terms |
| **WeatherAPI.com** (weatherapi.com/pricing.aspx) | page read, limits not extracted | key | free plan exists (the exact limits were not read) | worldwide | its own terms |

## Not checked
- **Accuracy itself.** No provider was measured against real observations here, and published comparisons were not read. Open-Meteo and MET Norway blend or run national and global models (Open-Meteo documents that it combines several national weather services' models), so the numbers are not independent of each other: for one place the difference between two such providers is often smaller than the error of the model. Whoever wants a real answer for Greece should compare a few days of forecasts of the candidate providers with the observations of the Hellenic National Meteorological Service for the same places, with a script (this was not done).
- Pirate Weather, Visual Crossing, Tomorrow.io, Meteostat, ECMWF open data: not read.
- Limits of WeatherAPI.com.

## Reading of the table
1. **Open-Meteo stays the sensible default**: no key, worldwide, enough calls for a ticker (the program asks once per city per refresh interval; a user with 20 cities and a 10 minute interval makes about 2 900 calls a day, under the 10 000 limit), CC BY 4.0. Its condition is **non-commercial use**: fine for this free program, a problem if the program were ever sold or bundled in a paid product.
2. **MET Norway** is the best second source: free without a key, worldwide, clear rules (identify the program with a User-Agent that has a contact address: it would have to be an address of the owner, which is a decision, not a default).
3. **NWS** only for the United States; it needs a separate code path and only helps US places.
4. **OpenWeather** and **WeatherAPI.com** need a key: a key shipped inside a free program is shared by every user and runs out; the user would have to enter their own, which is a setting plus a privacy note (the key is a secret, stored like the Anthropic key, DPAPI).

## Location (opt-in), as the request describes
Not built yet. The rules that follow from the request: ask first (opt-in), use the Windows location service or a city the user types, round the coordinates (two decimals, about 1 km) before they are sent to any provider, never store the exact position (only city or area), and say in the README which provider receives it. Open-Meteo already receives only the coordinates of the cities in the user's own list.

## Decisions needed from the owner
1. Keep Open-Meteo as the only provider, or add MET Norway as a second source (and with which contact address in the User-Agent)?
2. Is a "commercial use" of the program planned? (It decides whether Open-Meteo's free API may be used at all.)
3. Should a comparison against observations for Greek cities be run before choosing?
