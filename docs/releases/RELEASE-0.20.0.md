# Opsis News Crawler 0.20.0

## Ελληνικά
- **Έξυπνη ανίχνευση πραγματικών έκτακτων ειδήσεων.** Η μπάρα δεν γίνεται πια κόκκινη επειδή ένας τίτλος έχει τη λέξη «ΕΚΤΑΚΤΟ». Κάθε είδηση βαθμολογείται από πολλά σήματα: επιβεβαίωση από ανεξάρτητους εκδότες σε λίγα λεπτά, ταχύτητα, φρεσκάδα, αξιοπιστία πηγής (πρακτορεία, επίσημοι φορείς), αντικειμενικά δεδομένα καταστροφών (μέγεθος, επίπεδο συναγερμού) και πολύ μικρό βάρος για λέξεις-κλειδιά. Φιλτράρονται απόψεις/αναλύσεις/συνεντεύξεις/λίστες, υπερβολές αθλητικών και showbiz, παλιά γεγονότα, προαναγγελίες και clickbait.
- **Ευαισθησία** (Αυστηρή / Κανονική / Ευαίσθητη) στις ρυθμίσεις· το πεδίο λέξεων έγινε «Επιπλέον λέξεις που με ενδιαφέρουν».
- **Φαίνεται ο λόγος** στην κάρτα (π.χ. «3 πηγές σε 4 λεπτά · επίσημη πηγή ή πρακτορείο»).
- **Μαθαίνει τοπικά** (χωρίς cloud): δεξί κλικ σε έκτακτο τίτλο → «Αυτό δεν είναι έκτακτο», «Σίγαση πηγής», «Σίγαση θέματος»· «Να ξεχάσει τις επιλογές μου» στις ρυθμίσεις.
- Ήχος, κόκκινη μπάρα και σύνδεσμος ζωντανού βίντεο μόνο όταν περνά το όριο.
- Έλεγχος: 65 επισημασμένα παραδείγματα σε 14 γλώσσες με precision/recall στο CI.

## English
- **Smart detection of real breaking news.** The bar no longer turns red because one headline contains a word like "BREAKING". Every story is scored from many signals: confirmation by independent publishers within minutes, speed, freshness, source reliability (agencies, official bodies), objective disaster data (magnitude, alert level) and only a small weight for keywords. Opinion / analysis / interviews / lists, sports and showbiz hyperbole, old events, announcements and clickbait are filtered out.
- **Sensitivity** (Strict / Normal / Sensitive) in the settings; the keyword box is now "Extra words I care about".
- **The reason is shown** on the card (e.g. "3 sources in 4 min · official or agency source").
- **It learns locally** (no cloud): right-click an urgent headline → "This is not breaking news", "Mute this source", "Mute this topic"; "Forget my choices" in the settings.
- Sound, red bar and live-video link only when the threshold is passed.
- Tested on 65 labelled examples in 14 languages with precision / recall in CI.
