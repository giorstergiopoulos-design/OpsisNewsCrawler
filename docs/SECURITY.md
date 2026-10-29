# Security audit and trust model — Opsis News Crawler (2026-10-29, version 0.16.2)

Scope: TASKS-common point 9. Method: reading every place that downloads, launches, parses or stores something (`grep` for `HttpClient`, `Process.Start`, `XmlReader`, `Assembly`, `Registry`, `Log.Write`), `dotnet list package --vulnerable --include-transitive`, the installer script, and unit tests for every rule that could be made pure (`DownloadPolicyTests`, `SuiteTests`, `UpdateAnnounceTests`).

## Trust model
- The program runs **as the normal user, never elevated**. The installer is `PrivilegesRequired=lowest` (per-user folder, `HKCU` only, no `PATH`, no services, no drivers, no firewall rules, no `HKLM`).
- Only one thing runs elevated by Windows itself and only if the user chose it: nothing. There is no elevated helper.
- **Updates** come only from this repository's GitHub releases. The user opts in (`CheckForUpdates`). The setup is verified against the SHA-256 that GitHub publishes for the asset (or the `.sha256` file next to it) and deleted on a mismatch. Trust root = the GitHub account and TLS; there is no code-signing certificate yet, so Windows SmartScreen will warn (open item O1).
- **Family downloads** (Opsis suite window) go to the Downloads folder after the user confirmed name, size and source; the file is **never started** by the program.
- **Plugin**: the Crawler does not load plugins. The Opsis host loads `OpsisNewsCrawler.OpsisPlugin.dll` from the folder the installer registered under `HKCU\Software\Opsis\PluginPaths` / `%AppData%\Opsis\plugins`. Anyone who can write to that folder (= the same Windows user, or an administrator) can run code inside Opsis; that is the plugin model of the host and is the same trust as the user's own `Startup` folder. The plugin contract assembly keeps a frozen version; the host (Opsis) is responsible for any signature or hash policy on plugin DLLs (open item O2, noted to the coordinator).
- **Secrets**: the user's own AI key is stored encrypted with DPAPI (current user) and never exported; it travels only in the `x-api-key` header to `api.anthropic.com`.

## Found / fixed / open

| # | Finding | Status |
|---|---|---|
| F1 | Update and family downloads checked only the *first* URL; a redirect could end on another host | **Fixed** — the final address must be `https` on GitHub or its CDN (`DownloadPolicy.IsTrustedRedirectTarget`), otherwise the download stops before one byte is saved |
| F2 | The `.sha256` file was read without a size limit (`GetStringAsync`) | **Fixed** — at most 4 KB are read (`ReadLimitedTextAsync`) |
| F3 | The update setup was written straight to its final name before the hash was checked | **Fixed** — written to `.part`, renamed only after the hash matches; a mismatch deletes it |
| F4 | WebView2 bootstrapper signature check used `-like '*O=Microsoft Corporation*'` (matches "Not Microsoft Corporation Ltd") and put the path into the script text | **Fixed** — exact organisation match (`IsMicrosoftSigner`), path passed in an environment variable, temp folder deleted afterwards (also when the signature is wrong) |
| F5 | Asset names from the release JSON were only checked for invalid characters (`..` passed) | **Fixed** — `IsSafeFileName` (no `.`/`..`, no trailing dot or space, max 120) in the catalogue parser and again in the downloader |
| F6 | "Open" in the family window started any path found in the registry | **Fixed** — only an existing `.exe` is started |
| F7 | `crawler.log` could contain the Windows account name in file paths of exception texts | **Fixed** — the profile folder is written as `%USERPROFILE%` |
| F8 | The GDACS feed was parsed with `XDocument.Parse` (DTDs allowed: entity-expansion attack); GeoSearch and feed discovery had no response size cap | **Fixed** — `DtdProcessing.Prohibit`, 8 MB character cap, 2 MB / 4 MB response caps; test with an entity bomb |
| — | NuGet vulnerabilities (`dotnet list package --vulnerable --include-transitive`) | **None** in all 6 projects |
| — | XML: RSS/OPML readers use `DtdProcessing.Ignore` + `XmlResolver = null`, feeds capped at `MaxFeedBytes`; JSON parsers use `JsonDocument` with depth limits for the catalogue | Checked, no change needed |
| — | `Process.Start(UseShellExecute=true)` is used only with `http(s)` addresses that pass `Uri` parsing, or `github.com` addresses of the catalogue; `notepad.exe`/`explorer.exe` get one quoted path | Checked |
| — | WebView2 (live preview, weather map): no dev tools, no host objects, no web messages, new windows and downloads blocked, permissions denied, navigation limited to `http(s)`/`about` (preview) and `data`/`about` (map) | Checked |
| — | Zip-slip: the program never extracts archives (portable zip is made by the release script only) | Not applicable |
| — | Installer: no `HKLM`, no `PATH`, uninstall removes the `Run` value and the program folder; the settings folder `%AppData%\OpsisNewsCrawler` is kept on purpose (user data) | Checked |
| O1 | No Authenticode signature on `OpsisNewsCrawler-Setup.exe` / the exe | **Open** (needs a certificate; owner's decision) |
| O2 | Plugin DLL integrity is not verified by the host before loading | **Open** (belongs to Opsis `PluginHost`; reported to the coordinator) |
| O3 | Update trust root is GitHub itself: the hash comes from the same origin as the file | **Open by design** (would need a signature or a pinned key) |
| O4 | The setup is hash-checked twice (download and right before start) but a process of the same user could swap the file in between | **Open, low** (same-user attacker can already write to the program folder) |
