# Opsis News Crawler 0.13.5

> Το κύριο Opsis - Desktop Customization παραμένει στην έκδοση **1.9.8**. / The main application Opsis - Desktop Customization stays at version **1.9.8**.

## Ελληνικά
### Νέα
- **Κατάλογος Yahoo Finance** (Πηγές → Ροή αγορών): προαιρετικός, **ανενεργός από προεπιλογή, ανεπίσημος, με δική σου ευθύνη** (οι όροι του Yahoo δεν επιτρέπουν αυτοματοποιημένη χρήση). Αναζήτηση οποιασδήποτε μετοχής, δείκτη (και του Γενικού Δείκτη Αθηνών), νομίσματος, πρώτης ύλης, ETF ή κρυπτονομίσματος από οποιοδήποτε χρηματιστήριο, ή έτοιμες ομάδες με ένα κλικ. Τα προϊόντα εμφανίζονται στις μπάρες όπως οι άλλες τιμές, με ώρα (είναι καθυστερημένες).
- **Όσο πιο λίγη και «ήσυχη» κίνηση γίνεται**: το πολύ 30 προϊόντα· το πολύ μία αίτηση ανά προϊόν κάθε 15 λεπτά (ενδιάμεσα απαντά η προσωρινή μνήμη)· μία αναζήτηση = μία αίτηση, μόνο όταν πατάς «Αναζήτηση»· αιτήσεις η μία μετά την άλλη με μικρή καθυστέρηση· **παύση 6 ωρών στο πρώτο 401/403/429**· ειλικρινής αναγνώριση, χωρίς cookies, χωρίς παράκαμψη ορίων ή προστασιών. Το κείμενο της ρύθμισης δεν αναφέρει άλλες υπηρεσίες.
### Διορθώσεις
- **Στυλ μπάρας**: η στρογγυλεμένη και η pill μπάρα έφταναν και στις δύο άκρες της οθόνης, όπου οι καμπύλες δεν φαίνονταν. Τώρα η κύρια, η δεύτερη και οι μπάρες των οθονών αιωρούνται με περιθώριο 28 px και λεπτό περίγραμμα, ώστε το σχήμα να φαίνεται και όταν η μπάρα είναι διάφανη.
- Το `StoreKey` δεν γράφεται πια στο αρχείο ρυθμίσεων.
### Δεν έχουν δοκιμαστεί
Η πραγματική κλήση του βοηθού AI· η εγκατάσταση .NET/WebView2 από τον installer· η μπάρα σε δεύτερη οθόνη. Οι μεταφράσεις δεν έχουν ελεγχθεί από φυσικούς ομιλητές.

| | |
|---|---|
| ![Κατάλογος Yahoo](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/el/07-markets.png) | ![Μπάρα pill](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/bar-pill.png) |

## English
### New
- **Yahoo Finance catalogue** (Sources → Markets flow): optional, **off by default, unofficial, at your own risk** (Yahoo's terms do not allow automated use). Search any share, index (including the Athens General Index), currency, commodity, ETF or crypto from any exchange, or add ready-made groups with one click. The instruments appear on the bars like the other prices, with a time (they are delayed).
- **As little and as ordinary traffic as possible**: at most 30 instruments; at most one request per instrument every 15 minutes (a cache answers in between); one search = one request, only when you press Search; requests one after the other with a short pause; **a 6-hour stop at the first 401/403/429**; honest user agent, no cookies, no limit or protection is bypassed. The setting text mentions no other services.
### Fixes
- **Bar styles**: a rounded or pill bar spanned both screen edges, where the curves could not be seen. The main, second and monitor bars now float with a 28 px margin and a thin outline, so the shape is visible even when the bar is transparent.
- `StoreKey` is no longer written to the settings file.
### Not tried
A real call to the AI assistant; the .NET/WebView2 installation by the installer; a bar on a second monitor. The translations have not been reviewed by native speakers.

| | |
|---|---|
| ![Yahoo catalogue](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/en/07-markets.png) | ![Pill bar](https://raw.githubusercontent.com/giorstergiopoulos-design/OpsisNewsCrawler/main/docs/screenshots/bar-pill.png) |

Not code-signed: Windows SmartScreen may warn. Requires the .NET 10 Desktop Runtime (the installer can fetch it).
