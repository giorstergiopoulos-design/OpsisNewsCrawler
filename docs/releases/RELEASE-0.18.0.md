# Opsis News Crawler 0.18.0

## Ελληνικά

**Τι νέο υπάρχει**
- **Εικονίδιο ανά είδος καταστροφής** (σεισμός, πυρκαγιά, πλημμύρα, καταιγίδα, ηφαίστειο, ξηρασία, τσουνάμι, άλλο) στο χρώμα του επιπέδου συναγερμού.
- **Όλος ο κόσμος ανά ήπειρο**: κάθε ήπειρος ξεκινά με την εικόνα και το όνομά της και ακολουθούν τα γεγονότα της.
- **Κάρτα λεπτομερειών** με κλικ σε γεγονός: ζωντανός χάρτης, μέγεθος, βάθος, ώρα, επίπεδο, οι οργανισμοί που το αναφέρουν με σύνδεσμο και η επίσημη σελίδα.
- **Ένα χαλασμένο στοιχείο δεν χάνει τα υπόλοιπα**: οι πηγές διαβάζονται στοιχείο-στοιχείο και η μπάρα ξεχωρίζει «δεν συμβαίνει κάτι» από «λίστα ελλιπής».
- **Εγκατάσταση στο Program Files** (δικαιώματα διαχειριστή)· παλιά εγκατάσταση ανά χρήστη αφαιρείται αυτόματα, οι ρυθμίσεις μένουν. Plugin στο προστατευμένο φάκελο του Opsis όταν είναι στο Program Files.
- **Ανθεκτικότητα**: όλοι οι `async void` χειριστές πιάνουν κάθε σφάλμα· έλεγχοι timeouts/μεγέθους.
- Μεταφράσεις 14 γλωσσών.

**Για το 0.18.1**: σελίδες ρυθμίσεων σε στυλ Windows 11.
**Δεν έχει ελεγχθεί**: ο installer δεν εκτελέστηκε (μόνο μεταγλωττίστηκε), η αφαίρεση παλιάς εγκατάστασης δεν δοκιμάστηκε σε πραγματικό PC, ο χάρτης της κάρτας δεν άνοιξε ζωντανά (το HTML και τα εικονίδια ελέγχθηκαν), 150 % DPI.

## English

**What's new**
- **An icon for every kind of disaster** (earthquake, wildfire, flood, storm, volcano, drought, tsunami, other) in the colour of the alert level.
- **World by continent**: each continent starts with its picture and name, then its events.
- **Detail card** on click: live map, magnitude, depth, time, level, the organisations that report it with a link, and the official page.
- **A broken item no longer hides the rest**: feeds are read item by item and the bar tells "nothing is happening" from "the list is incomplete".
- **Installed in Program Files** (administrator rights); an earlier per-user install is removed automatically, settings stay. The plugin goes into Opsis' protected folder when Opsis is in Program Files.
- **Robustness**: every `async void` handler catches everything; timeouts and size limits checked.
- Translations in 14 languages.

**For 0.18.1**: settings pages in the Windows 11 style.
**Not verified**: the installer was compiled but not run, removing an old per-user install was not tried on a real PC, the card's live map was not opened (its HTML and the icons were checked), 150 % DPI.
