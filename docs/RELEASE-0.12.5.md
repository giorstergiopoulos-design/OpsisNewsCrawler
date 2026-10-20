# Opsis News Crawler 0.12.5

> Το κύριο Opsis - Desktop Customization παραμένει στην έκδοση **1.9.8**. / The main application Opsis - Desktop Customization stays at version **1.9.8**.

## Ελληνικά

### Συγχώνευση
Η έκδοση ενώνει όλη τη γραμμή 0.11.5.3 (ήχος ειδοποιήσεων, προειδοποιήσεις καιρού, χάρτης καιρού με κλικ, μπάρα επιφάνειας εργασίας, OPML,
εξαγωγή/εισαγωγή ρυθμίσεων, πάγωμα από το εικονίδιο) με τα 0.8–0.10 (καιρός, μπάρα ανά οθόνη, «Για αργότερα», ενημερώσεις, 14 γλώσσες).
Το πρόγραμμα χτίστηκε και δοκιμάστηκε: **56 αυτόματα tests** περνούν και όλες οι σελίδες ρυθμίσεων ελέγχθηκαν με στιγμιότυπα.

### Νέα
- **Νέο εικονίδιο** εφαρμογής (και στον installer) και **κινούμενο εικονίδιο** στην περιοχή ειδοποιήσεων: μικρή μπάρα όπου περνούν από δεξιά προς
  αριστερά οι ειδήσεις (εφημερίδα), τα αθλητικά (μπάλα), η άμυνα & διπλωματία (ασπίδα με κλαδί ελιάς) και η οικονομία (γράφημα και νομίσματα).
- **Δύο νέες κατηγορίες ειδήσεων**: «Άμυνα & Διπλωματία» (Defence Point, Defence Review, Militaire.gr, Breaking Defense, Defense News,
  POLITICO Europe) και «Αθλητικά» (Gazzetta, in.gr, και προαιρετικά BBC Sport, ESPN). Τα feeds ελέγχθηκαν ζωντανά.
- **Χαρτοφυλάκιο**: οι μετοχές που έχεις (ποσότητα, μέση τιμή), αξία και κέρδος/ζημιά σε ευρώ με ζωντανές τιμές, RSI και σήμα SMA20/50 από τις
  τιμές κλεισίματος που κρατά το πρόγραμμα (ιδέες από τα stonks-cli και greek-stocks-ai). Άνοιγμα από το εικονίδιο ή από Πηγές → Ροή αγορών.
- **Εξαρτήσεις**: σελίδα στον installer και στις Πληροφορίες (.NET 10 Desktop Runtime, WebView2). Αυτά που υπάρχουν φαίνονται τσεκαρισμένα και δεν
  αλλάζουν· τα υπόλοιπα κατεβαίνουν από τη Microsoft και τρέχουν μόνο με έγκυρη υπογραφή Microsoft.
- Το παράθυρο **καιρού/χάρτη** ανοίγει στη δεξιά άκρη της οθόνης και ακουμπά στο πάνω μέρος της μπάρας (ή της γραμμής εργασιών).
- Η μπάρα **μέσα στη γραμμή εργασιών** παίρνει το ύψος της και γλιστρά μαζί της όταν κρύβεται αυτόματα.

### Αλλαγές
- Η καρτέλα **Θέση** μπήκε στην καρτέλα **Μπάρες**· όλα τα πλαίσια έχουν το ίδιο πλάτος.
- Το plugin μετονομάστηκε σε `OpsisNewsCrawler.OpsisPlugin.dll` (πριν `MotionDeskPlugin`)· ο installer σβήνει το παλιό αρχείο.
- Η έκδοση συγκροτήματος του συμβολαίου plugin μένει `0.8.0.0` (αλλιώς το Opsis δεν φορτώνει plugin με νεότερο αριθμό)· το `tools\release.ps1` το ελέγχει.
- Το Pyth Hermes (πετρέλαιο/αέριο σχεδόν ζωντανά) απαντά πλέον 401 χωρίς κλειδί: το πρόγραμμα το αφήνει 12 ώρες και χρησιμοποιεί τις ημερήσιες τιμές EIA.

### Δεν έχουν δοκιμαστεί στο πραγματικό σύστημα
Η εγκατάσταση .NET/WebView2 από τον installer (ανιχνεύονται σωστά, αλλά δεν κατέβηκε τίποτα εδώ), η κίνηση της ενσωματωμένης μπάρας με αυτόματα
κρυφή γραμμή εργασιών, η μπάρα σε δεύτερη οθόνη. Οι πιο πολλές πηγές δεδομένων και τα feeds δοκιμάστηκαν ζωντανά.

### Στιγμιότυπα
| | |
|---|---|
| ![Μπάρες](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/el/02-bars-and-position.png) | ![Χαρτοφυλάκιο](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/el/12-portfolio.png) |
| ![Ειδήσεις](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/el/06-news-sources.png) | ![Εξαρτήσεις](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/el/14-dependencies.png) |

![Άμυνα](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/bar-defence-el.png)
![Αθλητικά](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/bar-sports-el.png)
![Εικονίδιο](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/icon-animation-frames.png)

## English

### Merge
This version joins the whole 0.11.5.3 line (alert sound, weather warnings, click-for-map weather, desktop bar, OPML, settings export/import,
snooze from the tray icon) with 0.8–0.10 (weather, a bar per monitor, "Read later", updates, 14 languages). It was built and tested:
**56 automated tests** pass and every settings page was checked with screenshots.

### New
- A **new application icon** (also in the installer) and an **animated notification-area icon**: a little bar on which the news (newspaper),
  sports (ball), defence & diplomacy (shield with an olive leaf) and economy (chart and coins) pass from right to left.
- **Two new news categories**: "Defence & Diplomacy" (Defence Point, Defence Review, Militaire.gr, Breaking Defense, Defense News, POLITICO
  Europe) and "Sports" (Gazzetta, in.gr, optionally BBC Sport, ESPN). The feeds were checked live.
- **Portfolio**: the shares you own (quantity, average price), value and profit/loss in euro with live prices, RSI and an SMA20/50 signal from
  the closing prices the program stores (ideas from stonks-cli and greek-stocks-ai). Open it from the tray icon or Sources → Markets flow.
- **Dependencies**: a page in the installer and in Information (.NET 10 Desktop Runtime, WebView2). What is already installed is shown ticked and
  cannot be changed; the rest is downloaded from Microsoft and run only with a valid Microsoft signature.
- The **weather/map** window opens at the far right edge of the screen and touches the top of the bar (or of the taskbar).
- The bar **inside the taskbar** takes the taskbar's height and slides with it when it hides automatically.

### Changes
- The **Position** tab is now part of the **Bars** tab; all cards have the same width.
- The plugin was renamed `OpsisNewsCrawler.OpsisPlugin.dll` (was `MotionDeskPlugin`); the installer removes the old file.
- The assembly version of the plugin contract stays `0.8.0.0` (otherwise Opsis cannot load a plugin with a newer number); `tools\release.ps1` checks it.
- Pyth Hermes (near-live oil/gas) now answers 401 without a key: the program leaves it alone for 12 hours and uses the daily EIA prices.

### Not tried on a real system
The .NET/WebView2 installation by the installer (detection works, nothing was downloaded here), the slide of the embedded bar with an
auto-hiding taskbar, the bar on a second monitor. Most data sources and the feeds were tested live.

### Screenshots
| | |
|---|---|
| ![Bars](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/en/02-bars-and-position.png) | ![Portfolio](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/en/12-portfolio.png) |
| ![News](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/en/06-news-sources.png) | ![Dependencies](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/en/14-dependencies.png) |

![Defence](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/bar-defence-en.png)
![Sports](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/bar-sports-en.png)

Weather data: Open-Meteo.com (CC BY 4.0). Not code-signed: Windows SmartScreen may warn. Requires the .NET 10 Desktop Runtime (the installer can fetch it).
