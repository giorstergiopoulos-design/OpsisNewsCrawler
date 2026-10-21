# Opsis News Crawler 0.13.8

> Το κύριο Opsis - Desktop Customization παραμένει στην έκδοση **1.9.8**. / The main application Opsis - Desktop Customization stays at version **1.9.8**.

## Ελληνικά
### Νέα
- **Ενημερώσεις όπως στον GearWin**: κάρτα με την τρέχουσα έκδοση, «Έλεγχος τώρα», «Λήψη & εγκατάσταση», «Τι νέο υπάρχει» και μπάρα προόδου· έλεγχος σε κάθε εκκίνηση και κάθε 12 ώρες (ενεργός από προεπιλογή, μία φορά και για τις υπάρχουσες ρυθμίσεις)· μία ειδοποίηση ανά έκδοση· επιβεβαίωση πριν από οτιδήποτε· μόνο τα releases αυτού του αποθετηρίου, με το SHA-256 που δημοσιεύει το GitHub (αλλιώς το αρχείο .sha256· χωρίς κανένα δεν τρέχει τίποτα και ανοίγει η σελίδα)· αν δεν ταιριάζει το αρχείο σβήνεται· το hash ελέγχεται ξανά πριν τρέξει ο installer, που τρέχει σιωπηλά αφού κλείσει το πρόγραμμα, και το πρόγραμμα ανοίγει ξανά. Το ίδιο υλοποίησε η συνεδρία του Opsis για την κύρια εφαρμογή.
- **MACD**: πλαίσιο κάτω από το γράφημα κεριών και τρίτη στρατηγική στη δοκιμή στρατηγικής.
- **Διάθεση ειδήσεων**: νέα σελίδα στην Ανάλυση αγορών. Απλές λίστες λέξεων στα ελληνικά και στα αγγλικά πάνω στους τίτλους (αναγνωρίζει την άρνηση), συνολική διάθεση, διάθεση ανά μετοχή και οι πιο έντονοι τίτλοι. Πρόχειρη ένδειξη, όχι πρόβλεψη ή επενδυτική συμβουλή.
- **Κινούμενο εικονίδιο** του προγράμματος στην κεφαλίδα των ρυθμίσεων (και στη σελίδα μέσα στο Opsis)· κινείται και το εικονίδιο της γραμμής τίτλου.
### Δοκιμές (πραγματικές)
- Ενημέρωση: έλεγχος και λήψη 3 MB από το πραγματικό release με σωστό hash, απόρριψη λάθος hash με διαγραφή αρχείου, και ο βοηθός σε δοκιμαστικό αντίγραφο: περιμένει το κλείσιμο, τρέχει σιωπηλά τον installer (το αρχείο αντικαταστάθηκε) και ανοίγει ξανά το πρόγραμμα.
- Εξαρτήσεις: λήψη του .NET 10 Desktop Runtime (57 MB) και του WebView2 από τη Microsoft και έλεγχος υπογραφής (έγκυρη, O=Microsoft Corporation). Δεν έγινε εγκατάσταση, γιατί είναι ήδη εγκατεστημένα.
- Η ειδοποίηση στο tray εμφανίζεται (δοκιμή). **Δεν δοκιμάστηκαν**: το κλικ πάνω της, η πραγματική κλήση του βοηθού AI (θέλει κλειδί), η μπάρα σε δεύτερη οθόνη (υπάρχει μία οθόνη).

| | |
|---|---|
| ![Ενημέρωση](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/el/20-updates.png) | ![Διάθεση ειδήσεων](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/el/18-analysis-mood.png) |
| ![MACD](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/el/19-analysis-macd.png) | |

Τα στιγμιότυπα της ανάλυσης έχουν **παραδειγματικά δεδομένα**.

## English
### New
- **Updates like GearWin**: a card with the current version, "Check now", "Download & install", "What's new" and a progress bar; a check at every start and every 12 hours (on by default, once also for existing settings); one notification per version; confirmation before anything happens; only this repository's releases, with the SHA-256 GitHub publishes (else the .sha256 file; with neither nothing runs and the page opens); a mismatch deletes the file; the hash is checked again before the installer runs, silently, after the program has ended, and the program opens again. The Opsis session did the same for the main application.
- **MACD**: a panel under the candlestick chart and a third back-test strategy.
- **News mood**: a new page in the market analysis. Simple Greek and English word lists on the headlines (negation aware), the overall mood, the mood per share and the strongest headlines. A rough guide, not a forecast or investment advice.
- **Animated program icon** in the settings header (and on the page inside Opsis); the title-bar icon moves too.
### Tests (real)
- Update: check and 3 MB download from the real release with a matching hash, a wrong hash refused and the file deleted, and the helper on a throwaway copy: it waits for the program to end, runs the installer silently (the file was replaced) and reopens the program.
- Dependencies: the .NET 10 Desktop Runtime (57 MB) and WebView2 were downloaded from Microsoft and the signature checked (valid, O=Microsoft Corporation). Not installed, because both are present.
- The tray notification shows (tested). **Not tested**: a click on it, a real call to the AI assistant (needs a key), a bar on a second monitor (one screen only).

| | |
|---|---|
| ![Update](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/en/20-updates.png) | ![News mood](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/en/18-analysis-mood.png) |
| ![MACD](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/en/19-analysis-macd.png) | |

The analysis screenshots use **made-up example data**. Not code-signed: Windows SmartScreen may warn. Requires the .NET 10 Desktop Runtime (the installer can fetch it).
