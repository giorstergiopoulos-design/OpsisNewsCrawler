# Opsis News Crawler 0.15.0

## Ελληνικά

**Τι νέο υπάρχει**

- **Οδηγός καλωσορίσματος** με κινούμενο εικονίδιο: έξι σύντομες σελίδες στην πρώτη εκκίνηση νέας έκδοσης (όχι μέσα στο Opsis), από το μενού δίπλα στο ρολόι, με «Να μην ξαναεμφανιστεί». Όταν κλείνει, μικραίνει μέσα στο εικονίδιο των ρυθμίσεων ή στην περιοχή ειδοποιήσεων.
- **Η οικογένεια Opsis**: οι εφαρμογές του ίδιου δημιουργού με κατάσταση εγκατάστασης και «Υπάρχει ενημέρωση». Η λήψη γίνεται μόνο μετά από επιβεβαίωση, ελέγχεται με SHA-256 και δεν εκτελείται ποτέ από το πρόγραμμα.
- **Κινούμενο εικονίδιο με έξι σύμβολα**: εφημερίδα, μπάλα, ασπίδα, οικονομία, **καιρός**, **φυσικές καταστροφές** (στην περιοχή ειδοποιήσεων, στην κεφαλίδα των ρυθμίσεων, στα splash και στο εικονίδιο του προγράμματος).
- **Ειδοποίηση νέας έκδοσης** μία φορά ανά έκδοση (30 s μετά την εκκίνηση, μετά ανά 24 ώρες, με ETag· όχι σε πλήρη οθόνη, όχι μέσα στο Opsis).
- **Θέματα** ανεξάρτητα από το Opsis (Midnight, Forest, Dawn, Contrast, χρώμα έμφασης των Windows), επιλογή «Ακολούθηση Opsis».
- **Πληκτρολόγιο**: τα κουμπιά, οι λίστες, τα ρυθμιστικά και τα checkbox λειτουργούν πλέον με Tab, βέλη, Space/Enter και δείχνουν πού είναι η εστίαση.
- **Διορθώσεις**: η μπάρα επιφάνειας αντικαθιστά σωστά κρυμμένη μπάρα εργασιών των Windows 11 και σέβεται το «Να μένει πάνω από τα άλλα παράθυρα»· δώδεκα αρχεία γλώσσας της 0.14.7 ήταν χαλασμένα (τα μενού έδειχναν ακατέργαστα κλειδιά).

**Αρχεία**

| Αρχείο | Τι είναι |
|---|---|
| `OpsisNewsCrawler-Setup.exe` | Installer (Inno Setup 7, 14 γλώσσες, εικόνες οδηγού από το εικονίδιο της εφαρμογής) |
| `OpsisNewsCrawler-Setup.exe.sha256` | SHA-256 του installer |
| `OpsisNewsCrawler-0.15.0-portable.zip` | Φορητή έκδοση (χρειάζεται .NET 10 Desktop Runtime, χωρίς plugin του Opsis) |
| `OpsisNewsCrawler-0.15.0-portable.zip.sha256` | SHA-256 της φορητής έκδοσης |

**Έλεγχος**: `(Get-FileHash .\OpsisNewsCrawler-Setup.exe -Algorithm SHA256).Hash.ToLower()` και σύγκριση με το `.sha256`. Οι installers δεν είναι ψηφιακά υπογεγραμμένοι.

**Δεν έχει ελεγχθεί**: διάταξη των ρυθμίσεων στο 150 % και 200 % κλιμάκωση συστήματος, αραβική διάταξη κάθε σελίδας, πραγματική λήψη εφαρμογής από την οικογένεια Opsis, και η σελίδα ρυθμίσεων μέσα στο ζωντανό Opsis.

## English

**What's new**

- **Welcome guide** with the animated icon: six short pages on the first start of a new version (not inside Opsis), also from the tray menu, with "Don't show again". When closed it shrinks into the settings icon or the notification area.
- **The Opsis family**: the apps of the same author with installed state and "Update available". A download needs your confirmation, is checked with SHA-256 and is never run by the program.
- **Animated icon with six symbols**: newspaper, ball, shield, economy, **weather**, **natural disasters** (tray, settings header, splash screens and the program icon).
- **Update notice** once per version (30 s after the start, then every 24 h, with an ETag; not in full screen, not inside Opsis).
- **Themes** independent of Opsis (Midnight, Forest, Dawn, Contrast, Windows accent colour), plus an explicit "Follow Opsis".
- **Keyboard**: buttons, drop-down lists, sliders and check boxes now work with Tab, arrow keys, Space/Enter and show where the focus is.
- **Fixes**: the desktop bar correctly stands in for a hidden Windows 11 taskbar and respects "Keep it above other windows"; twelve language files of 0.14.7 were broken (menus showed raw keys).

**Files**

| File | What |
|---|---|
| `OpsisNewsCrawler-Setup.exe` | Installer (Inno Setup 7, 14 languages, wizard pictures drawn from the app icon) |
| `OpsisNewsCrawler-Setup.exe.sha256` | SHA-256 of the installer |
| `OpsisNewsCrawler-0.15.0-portable.zip` | Portable build (needs the .NET 10 Desktop Runtime, no Opsis plugin) |
| `OpsisNewsCrawler-0.15.0-portable.zip.sha256` | SHA-256 of the portable build |

**Verify**: `(Get-FileHash .\OpsisNewsCrawler-Setup.exe -Algorithm SHA256).Hash.ToLower()` and compare with the `.sha256` file. The installers are not code-signed.

**Not verified**: settings layout at 150 % and 200 % system scaling, Arabic layout of every page, a real app download from the Opsis family window, and the settings page inside a live Opsis.
