# Opsis News Crawler 0.13.1

> Το κύριο Opsis - Desktop Customization παραμένει στην έκδοση **1.9.8**. / The main application Opsis - Desktop Customization stays at version **1.9.8**.

## Ελληνικά

### Νέα
- **Προαιρετικό ιστορικό τιμών από το Yahoo Finance** (Πηγές → Ροή αγορών → Ιστορικό τιμών). **Ανενεργό από προεπιλογή, ανεπίσημο, με δική σου ευθύνη**: οι όροι του Yahoo δεν επιτρέπουν αυτοματοποιημένη χρήση. Όταν το ενεργοποιήσεις, το πρόγραμμα ζητά τις ημερήσιες τιμές των τελευταίων έξι μηνών για τα προϊόντα σου (μία αίτηση ανά προϊόν την ημέρα, με διάστημα ενός δευτερολέπτου), σταματά με την πρώτη άρνηση και δεν παρακάμπτει κανένα όριο. Έτσι το **γράφημα κεριών, η δοκιμή στρατηγικής και ο δείκτης φόβου/απληστίας** δουλεύουν από την πρώτη μέρα, με πραγματικά δεδομένα (στιγμιότυπα παρακάτω).
- **Ζώνες Bollinger** στο γράφημα κεριών.
- **Μεταφράσεις**: όλα τα νέα κείμενα (ανάλυση αγορών, βοηθός, στυλ μπάρας, tray, καιρός, μηνύματα δικτύου) στις 12 γλώσσες. Τα μεγάλα κείμενα Βοήθειας/Οδηγιών/Ιστορικού/Άδειας μένουν αγγλικά στις 12 αυτές γλώσσες.
- Ενημερωμένες Οδηγίες (νέα δομή ρυθμίσεων, ανάλυση αγορών).

### Διορθώσεις
- Οι μετοχές άλλων αγορών Euronext και το χαρτοφυλάκιο βρίσκουν πλέον τις αποθηκευμένες τιμές τους στα γραφήματα και στους δείκτες.

### Σημειώσεις
- Το **Stooq δεν υποστηρίζεται**: σήμερα απαντά με σελίδα επαλήθευσης «δεν είσαι ρομπότ» και το robots.txt του απαγορεύει την αυτόματη πρόσβαση· δεν παρακάμπτεται προστασία.
- Η πραγματική κλήση του βοηθού AI δεν έχει δοκιμαστεί (χρειάζεται δικό σου κλειδί Anthropic).

### Στιγμιότυπα (πραγματικά δεδομένα Yahoo, μετοχές Αθηνών)
| | |
|---|---|
| ![Ιστορικό τιμών](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/el/07-markets.png) | ![Κεριά](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/el/15-analysis-candles.png) |
| ![Δοκιμή](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/el/16-analysis-backtest.png) | ![Φόβος και απληστία](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/el/17-analysis-fear-greed.png) |

## English

### New
- **Optional price history from Yahoo Finance** (Sources → Markets flow → Price history). **Off by default, unofficial, at your own risk**: Yahoo's terms do not allow automated use. When switched on, the program asks for the daily prices of the last six months for your instruments (one request per instrument and day, one second apart), stops at the first refusal and bypasses no limit. This makes the **candlestick chart, the back-test and the fear & greed gauge** work from the first day, with real data (screenshots below).
- **Bollinger bands** on the candlestick chart.
- **Translations**: every new text (market analysis, assistant, bar styles, tray, weather, network messages) in the 12 other languages. The long Help/Instructions/History/License texts stay English in those 12 languages.
- Updated Instructions (new settings layout, market analysis).

### Fixes
- Shares of other Euronext markets and the portfolio now find their stored prices in the charts and indicators.

### Notes
- **Stooq is not supported**: it now answers with a "verify you are not a robot" page and its robots.txt forbids automated access; no protection is bypassed.
- A real call to the AI assistant has not been tested (it needs your own Anthropic key).

### Screenshots (real Yahoo data, Athens shares)
| | |
|---|---|
| ![Price history](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/en/07-markets.png) | ![Candles](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/en/15-analysis-candles.png) |
| ![Back-test](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/en/16-analysis-backtest.png) | ![Fear and greed](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/en/17-analysis-fear-greed.png) |

Weather data: Open-Meteo.com (CC BY 4.0). Not code-signed: Windows SmartScreen may warn. Requires the .NET 10 Desktop Runtime (the installer can fetch it).
