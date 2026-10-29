# Opsis News Crawler 0.16.2

## Ελληνικά

**Τι νέο υπάρχει**
- **Έλεγχος ασφαλείας** (αναφορά στο `docs/SECURITY.md`): οι λήψεις ενημερώσεων και εγκαταστάσεων της οικογένειας σταματούν αν η τελική διεύθυνση μετά τις ανακατευθύνσεις δεν είναι GitHub· το αρχείο checksum διαβάζεται με όριο 4 KB· το setup ενημέρωσης αποθηκεύεται ως `.part` και μετονομάζεται μόνο όταν ταιριάξει το SHA-256· το WebView2 πρέπει να είναι υπογεγραμμένο ακριβώς από «Microsoft Corporation»· ονόματα αρχείων χωρίς `..`· η ροή GDACS διαβάζεται χωρίς DTD· όρια μεγέθους σε γεωκωδικοποίηση και ανακάλυψη feeds· το `crawler.log` δεν περιέχει πια το όνομα λογαριασμού των Windows. Κανένα ευάλωτο πακέτο NuGet.
- **Ανοιχτά**: δεν υπάρχει πιστοποιητικό υπογραφής κώδικα· η ακεραιότητα του plugin ελέγχεται από το Opsis· η ρίζα εμπιστοσύνης των ενημερώσεων είναι το ίδιο το GitHub.

**Δεν έχει ελεγχθεί**: ο πλήρης κύκλος ενημέρωσης με πραγματικό release (τα μέρη του καλύπτονται από τεστ), ζωντανή επαλήθευση στην επιφάνεια εργασίας του ιδιοκτήτη.

## English

**What's new**
- **Security audit** (report in `docs/SECURITY.md`): update and family downloads stop unless the final address after redirects is GitHub; the checksum file is read with a 4 KB limit; the update setup is saved as `.part` and renamed only after the SHA-256 matches; WebView2 must be signed by exactly "Microsoft Corporation"; file names without `..`; the GDACS feed is parsed without DTDs; size caps on geocoding and feed discovery; `crawler.log` no longer contains the Windows account name. No vulnerable NuGet packages.
- **Open**: no code-signing certificate; plugin integrity is checked by the host (Opsis); the update trust root is GitHub itself.

**Not verified**: the complete update cycle with a real release (its parts are covered by tests), live check on the owner's desktop.
