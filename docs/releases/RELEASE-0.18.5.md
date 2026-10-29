# Opsis News Crawler 0.18.5

## Ελληνικά

**Τι νέο υπάρχει**
- **Ρυθμίσεις σε στυλ Windows 11**: οι επιλογές ναι/όχι είναι διακόπτες (ετικέτα αριστερά, διακόπτης δεξιά), οι βασικές με μία πρόταση περιγραφής· κάθε κάρτα έχει σύνδεσμο **Επαναφορά** μόνο για τα δικά της controls (δεν αποθηκεύεται τίποτα μέχρι το Εφαρμογή)· σημείωση ότι κάθε αλλαγή ισχύει με το Εφαρμογή και καμία ρύθμιση δεν θέλει επανεκκίνηση.
- **Ένα Crawler τη φορά**: το αυτόνομο πρόγραμμα και το plugin του Opsis μοιράζονται ένα mutex· δεν τρέχουν ποτέ δύο μπάρες.
- **Έλεγχος ποιότητας**: `tools/ci-audit.ps1` (έλεγχος ρυθμίσεων + διάταξης σε en/el/de/ar) τρέχει στο GitHub Actions σε κάθε push.
- **Επαληθεύτηκε ζωντανά**: η κάρτα λεπτομερειών με πραγματικό χάρτη OpenStreetMap.
- **Κοινά θέματα**: τα επώνυμα θέματα (Midnight, Forest, Dawn, Contrast) προέρχονται από το κοινό αρχείο θεμάτων του Opsis.UI.
- Μεταφράσεις 14 γλωσσών.

**Για το 0.18.6**: εκτέλεση του installer σε εικονική μηχανή (UAC, αφαίρεση παλιάς εγκατάστασης, εναλλαγή τρόπου εγκατάστασης), «Αργότερα / Παράλειψη έκδοσης / Τι νέο υπάρχει» στην ειδοποίηση ενημέρωσης, περιγραφές στους υπόλοιπους διακόπτες.
**Δεν έχει ελεγχθεί**: ο installer δεν εκτελέστηκε· 150 % DPI στις ρυθμίσεις· το plugin μέσα σε πραγματικό Opsis.

## English

**What's new**
- **Settings in the Windows 11 style**: yes/no settings are switches (label left, switch right), the main ones with a one-sentence description; every card has a **Reset** link for its own controls only (nothing is saved until Apply); a note says every change takes effect with Apply and no setting needs a restart.
- **One crawler at a time**: the stand-alone program and the Opsis plugin share one mutex; two bars never run.
- **Quality gate**: `tools/ci-audit.ps1` (settings + layout audit in en/el/de/ar) runs in GitHub Actions on every push.
- **Verified live**: the detail card with a real OpenStreetMap map.
- **Shared themes**: the named themes (Midnight, Forest, Dawn, Contrast) come from the shared theme file of Opsis.UI.
- Translations in 14 languages.

**For 0.18.6**: run the setup on a disposable machine (UAC, removing an old per-user install, switching install mode), "Later / Skip this version / What's new" in the update notification, descriptions under the remaining switches.
**Not verified**: the setup was not run; 150 % DPI of the settings; the plugin inside a real Opsis.
