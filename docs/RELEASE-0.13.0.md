# Opsis News Crawler 0.13.0

> Το κύριο Opsis - Desktop Customization παραμένει στην έκδοση **1.9.8**. / The main application Opsis - Desktop Customization stays at version **1.9.8**.

## Ελληνικά

### Νέα
- **Ανάλυση αγορών** (εικονίδιο tray, Πηγές → Ροή αγορών):
  - **Γράφημα κεριών** με μέσους SMA20/SMA50 και τιμές Ο/H/L/C στο hover. Για τις μετοχές Αθηνών τα στοιχεία είναι ακριβή (από το αρχείο συναλλαγών της ημέρας)· για τα υπόλοιπα προέρχονται από τις ανανεώσεις του προγράμματος και φαίνονται κούφια.
  - **Δοκιμή στρατηγικής** (διασταύρωση SMA ή RSI): απόδοση, αγορά και κράτημα, ποσοστό επιτυχίας, μέγιστη πτώση, λίστα συναλλαγών. Το σήμα εκτελείται στο κλείσιμο της επόμενης ημέρας· χωρίς προμήθειες.
  - **Φόβος και απληστία**: δείκτης 0–100 σε πέντε μέρη, υπολογισμένος από τις αποθηκευμένες τιμές των προϊόντων που παρακολουθείς (δεν είναι ο δείκτης του CNN).
  - **Βοηθός**: προαιρετικός, με το δικό σου κλειδί API της Anthropic. Το κλειδί αποθηκεύεται κρυπτογραφημένο για τον λογαριασμό σου στα Windows και δεν μπαίνει στην εξαγωγή ρυθμίσεων· τίποτα δεν στέλνεται μέχρι να πατήσεις Αποστολή, και οι τιμές/χαρτοφυλάκιό σου μόνο αν το επιλέξεις.
- **Παράθυρο ρυθμίσεων**: μία πλευρική στήλη πλοήγησης (ομάδες Ρυθμίσεις / Πηγές / Πληροφορίες, με αναζήτηση) αντί για δύο σειρές κουμπιών. Η σελίδα **Μπάρες** έχει πλέον όλες τις ρυθμίσεις μπαρών σε σαφείς ενότητες: θέση και συμπεριφορά, τι δείχνει κάθε μπάρα, μπάρα επιφάνειας εργασίας.
- **Μπάρα επιφάνειας εργασίας**: νέο στυλ — ορθογώνιο, στρογγυλεμένες γωνίες ή **κάψουλα (pill)**.
- **Προεπισκόπηση μπαρών**: μικρή οθόνη με κάθε μπάρα στη θέση της (και η μπάρα επιφάνειας εργασίας) και μεγέθυνση κάθε μπάρας σε αναγνώσιμο μέγεθος.
- **Εικονίδιο tray**: ένα στοιχείο Παύση/Συνέχεια. Στο μενού tray του Opsis προστέθηκαν **Κλείσιμο** και **Άνοιγμα** του crawler (χρειάζονται το νέο plugin).

### Σημαντικό
Το ιστορικό τιμών μεγαλώνει μία ημέρα για κάθε μέρα που τρέχει το πρόγραμμα (το Yahoo και το Stooq δεν επιτρέπονται από τους όρους τους), οπότε ο back-test και ο δείκτης χρειάζονται λίγες εβδομάδες δεδομένων. Τα στιγμιότυπα ανάλυσης παρακάτω είναι με **παραδειγματικά, φτιαγμένα δεδομένα**. Δεν έχει δοκιμαστεί πραγματική κλήση προς το API της Anthropic.

### Στιγμιότυπα
| | |
|---|---|
| ![Μπάρες](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/el/02-bars-and-position.png) | ![Κεριά](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/el/15-analysis-candles.png) |
| ![Δοκιμή](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/el/16-analysis-backtest.png) | ![Φόβος και απληστία](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/el/17-analysis-fear-greed.png) |

## English

### New
- **Market analysis** (tray icon, Sources → Markets flow):
  - **Candlestick chart** with SMA20/SMA50 and open/high/low/close on hover. Athens shares have exact figures (from the day's trade file); other instruments are sampled from the program's refreshes and drawn hollow.
  - **Back-test** (SMA crossover or RSI): return, buy & hold, win rate, max drawdown, trade list. A signal is traded at the next day's close; no fees.
  - **Fear & greed**: a 0–100 gauge in five parts computed from the stored prices of the instruments you watch (not the CNN index).
  - **Assistant**: optional, with your own Anthropic API key. The key is stored encrypted for your Windows account and left out of the settings export; nothing is sent until you press Send, and your prices/portfolio only if you tick that.
- **Settings window**: one left navigation rail (groups Settings / Sources / Information, with search) instead of two rows of buttons. The **Bars** page now holds every bar setting in clear sections: placement and behaviour, what each bar shows, the desktop bar.
- **Desktop bar**: new style — rectangle, rounded corners or **pill (capsule)**.
- **Bar preview**: a miniature screen with every bar where it sits (the desktop bar too) and a close-up of each bar at readable size.
- **Tray icon**: a single Pause/Resume item. The Opsis tray menu gets **Close** and **Open** for the crawler (needs the new plugin).

### Important
The price history grows by one day for each day the program runs (Yahoo and Stooq are not allowed by their terms), so the back-test and the gauge need a few weeks of data. The analysis screenshots below use **made-up example data**. A real call to the Anthropic API has not been tested.

### Screenshots
| | |
|---|---|
| ![Bars](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/en/02-bars-and-position.png) | ![Candles](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/en/15-analysis-candles.png) |
| ![Back-test](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/en/16-analysis-backtest.png) | ![Fear and greed](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/en/17-analysis-fear-greed.png) |

Weather data: Open-Meteo.com (CC BY 4.0). Not code-signed: Windows SmartScreen may warn. Requires the .NET 10 Desktop Runtime (the installer can fetch it).
