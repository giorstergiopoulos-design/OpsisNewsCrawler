# Opsis News Crawler 0.14.0

## English
- **Low-power mode inside Opsis**: when Opsis reports the Low hardware tier (weak PC), the bar scrolls at ~30 fps instead of ~66 and polls the taskbar every ~90 ms instead of 30 ms. The scroll is time based, so the speed you see is unchanged; CPU use drops.
- The plugin contract (`OpsisNewsCrawler.Plugin.Abstractions`) now contains the optional `IPluginProfileAware`, which the Opsis host already used. Plugins that do not implement it keep working.

**Tests**: build with 0 warnings/0 errors, 94 unit tests pass. The low-power timers were not measured on a real low-end PC (only the timer intervals are covered).

## Ελληνικά
- **Λειτουργία χαμηλής κατανάλωσης μέσα στο Opsis**: όταν το Opsis αναφέρει επίπεδο υλικού «Low» (αδύναμο PC), η μπάρα κυλά στα ~30 καρέ/δευτ. αντί για ~66 και ελέγχει το taskbar κάθε ~90 ms αντί για 30 ms. Η κύλιση βασίζεται στον χρόνο, άρα η ταχύτητα που βλέπεις δεν αλλάζει· πέφτει η χρήση CPU.
- Το συμβόλαιο plugin περιέχει πλέον το προαιρετικό `IPluginProfileAware` που ήδη χρησιμοποιούσε το Opsis.

SHA-256 of OpsisNewsCrawler-Setup.exe: `1d9a43c6c12320550089944fb3b4ea4aeaae01ae53258b45a7a52a8131e543d9`
