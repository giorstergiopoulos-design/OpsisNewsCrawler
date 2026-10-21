# Opsis News Crawler 0.13.2

> Το κύριο Opsis - Desktop Customization παραμένει στην έκδοση **1.9.8**. / The main application Opsis - Desktop Customization stays at version **1.9.8**.

## Ελληνικά
### Διορθώσεις
- **Στυλ μπάρας** (ορθογώνιο, στρογγυλεμένο, **pill**): δεν εφαρμόζονταν όταν το παράθυρο δημιουργούνταν αργότερα. Τώρα εφαρμόζονται πραγματικά (ελέγχθηκε στα παράθυρα) και υπάρχουν και για την κύρια, τη δεύτερη και τις μπάρες των οθονών (Μπάρες → Ακριβής θέση και συμπεριφορά).
- **Τιμές πετρελαίου και φυσικού αερίου**: το FRED απαντά πλέον με σελίδα επαλήθευσης «δεν είσαι ρομπότ» και οι τιμές Brent, WTI και Henry Hub έλειπαν. Εφεδρική πηγή οι δημόσιες σελίδες της αμερικανικής **EIA** (το robots.txt τους επιτρέπει)· οι τιμές είναι ημερήσιες, με ένδειξη ημερομηνίας.
### Αλλαγές
- Σελίδα **Μπάρες**: οι «Άλλες οθόνες» είναι πλέον η τελευταία ενότητα, μετά τη μπάρα επιφάνειας εργασίας.
- **Μεταφράσεις σε όλες τις 14 γλώσσες** για ό,τι ήταν μόνο ελληνικά/αγγλικά: ~100 κείμενα (ενότητες ρυθμίσεων, καιρικές συνθήκες, μενού άρθρου, ειδοποιήσεις, μηνύματα δοκιμής πηγών), η **Βοήθεια**, οι **Οδηγίες** και το πρόσφατο ιστορικό. Αγγλικά μένουν το παλιότερο ιστορικό, η άδεια MIT και τα ονόματα εταιρειών/πόλεων.
- Έλεγχος ρυθμίσεων: όλα τα στοιχεία του πάνελ δοκιμάστηκαν μέσα από κώδικα σε όλες τις γλώσσες (χωρίς εξαιρέσεις ή ακατέργαστα κλειδιά), τα κουμπιά πατήθηκαν, και το «Εφαρμογή» αποθηκεύει και ξαναφορτώνει σωστά.
### Δεν έχουν δοκιμαστεί
Η πραγματική κλήση του βοηθού AI· η εγκατάσταση .NET/WebView2 από τον installer· η μπάρα σε δεύτερη οθόνη. Οι μεταφράσεις δεν έχουν ελεγχθεί από φυσικούς ομιλητές.

| | |
|---|---|
| ![Μπάρες](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/el/02-bars-and-position.png) | ![Αγορές](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/el/07-markets.png) |

## English
### Fixes
- **Bar styles** (rectangle, rounded, **pill**) were not applied when the window was created later. They now really apply (checked on the windows) and exist for the main, second and monitor bars too (Bars → Exact placement and behaviour).
- **Oil and natural gas prices**: FRED now answers with a "verify you are not a robot" page, so Brent, WTI and Henry Hub were missing. The public pages of the U.S. **EIA** are the fallback (their robots.txt allows it); the prices are daily and shown with their date.
### Changes
- **Bars** page: "Other monitors" is now the last section, after the desktop bar.
- **Translations into all 14 languages** of everything that was only Greek/English: ~100 texts (settings sections, weather conditions, article menu, alerts, feed-test messages), **Help**, **Instructions** and the recent history. Older history, the MIT licence and company/city names stay English.
- Settings QA: every control of the panel was driven in-process in all languages (no exceptions or raw keys), buttons were clicked, and Apply saves and reloads correctly.
### Not tried
A real call to the AI assistant; the .NET/WebView2 installation by the installer; a bar on a second monitor. The translations have not been reviewed by native speakers.

| | |
|---|---|
| ![Bars](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/en/02-bars-and-position.png) | ![Markets](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/en/07-markets.png) |

Not code-signed: Windows SmartScreen may warn. Requires the .NET 10 Desktop Runtime (the installer can fetch it).
