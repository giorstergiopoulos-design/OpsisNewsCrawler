# Opsis News Crawler 0.19.1

## Ελληνικά
- **Διόρθωση (πραγματικό bug μετάβασης): ο installer δεν αφαιρούσε ποτέ την παλιά εγκατάσταση ανά χρήστη.** Έψαχνε την εγγραφή απεγκατάστασης με κλειδί γραμμένο με δύο αγκύλες `{{` (τυπογραφικό λάθος· το `{{` είναι διαφυγή των τιμών `[Setup]`, όχι των Pascal strings), δεν την έβρισκε ποτέ και τίποτα από την αφαίρεση του 0.18.6 δεν έτρεχε. Βρέθηκε από το νέο e2e τεστ σε καθαρό μηχάνημα. Τώρα το κλειδί είναι αυτό του προγράμματος και 3 τεστ συγκρίνουν τις σταθερές του Pascal με του προγράμματος.
- **Διόρθωση: μετά την απεγκατάσταση έμενε εγγραφή εκκίνησης.** Ο uninstaller αφαιρεί πλέον την τιμή εκκίνησης (HKCU και HKLM).

## English
- **Fix (a real migration bug): the setup never removed an older per-user installation.** It looked for the per-user uninstall entry under a key with two opening braces `{{` (a typo: `{{` escapes `[Setup]` values, not Pascal strings), never found it, and none of the removal of 0.18.6 ever ran. Found by the new end-to-end test on a clean machine. The key is now the program's, and 3 tests compare the Pascal constants with the program's.
- **Fix: a start-up entry was left after uninstall.** The uninstaller now removes the start-up value (HKCU and HKLM).
