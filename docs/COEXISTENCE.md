# Standalone program and Opsis plugin together (0.18.5)

| Question | Answer |
|---|---|
| Two bars at once? | No. Both use the named mutex `OpsisNewsCrawler.SingleInstance` (`Core/InstanceGuard.cs`). Whoever starts first owns it. The plugin that finds the program running starts no bar and writes a line to `crawler.log`; the program that finds the plugin running (default instance) tells the user "already running (as its own program or inside Opsis)". Tests: `InstanceGuardTests`. |
| Same settings? | Yes, one file `%AppData%\OpsisNewsCrawler\config.json` for both; with one owner at a time there are no double writers. Colours: the crawler keeps its own theme choice; inside Opsis "Follow Opsis" is just one more choice (`Theme.SetColors`). |
| Double autostart? | The setup's "Start with Windows" task exists only for the separate install (`Check: IsSeparate`) and writes one value (`HKLM ...\Run`, since 0.18.0); the integrated install writes no start-up entry (Opsis starts the plugin). The 0.18.0 setup also removes the old per-user value. |
| Shared registry keys / files? | `HKCU\Software\Opsis\PluginPaths` (value `OpsisNewsCrawler`) is only deleted by the setup (old builds wrote it); plugin folder: `<Opsis folder>\plugins\Opsis - Desktop Customization` (Opsis in Program Files) or `%AppData%\Opsis\plugins\...`. Nothing else is shared. |
| Setup while the program or Opsis runs | `AppMutex=OpsisNewsCrawler.SingleInstance` makes the setup ask to close it (the plugin holds the same mutex while Opsis runs the crawler). |
| Upgrade / uninstall | Same `AppId` for both modes: an install in the other mode is an upgrade of the same entry. **NOT VERIFIED**: switching mode over an existing install (the previous folder may be proposed again; the old files of the other mode are not removed); uninstall in each combination. To be tested on a disposable Windows VM. |
| Quality gate | `tools/ci-audit.ps1` (settings audit + layout audit in en/el/de/ar) runs in GitHub Actions on every push. |
