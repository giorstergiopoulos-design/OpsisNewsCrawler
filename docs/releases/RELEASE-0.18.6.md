# Opsis News Crawler 0.18.6

## Ελληνικά

**Τι διορθώνεται**
- **Η ενημέρωση από το 0.17.0 άφηνε δύο αντίγραφα και δεν ενημέρωνε.** Ο installer αφαιρεί πρώτα το παλιό αντίγραφο ανά χρήστη και ελέγχει το αποτέλεσμα: τερματίζει μόνο το πρόγραμμα του συγκεκριμένου φακέλου, τρέχει τον uninstaller του, επαληθεύει ότι έφυγαν η εγγραφή και ο φάκελος, καθαρίζει τα υπολείμματα και σταματά με μήνυμα αν μείνει κάτι.
- **Ενημέρωση**: το setup ξεκινά με ρητό αίτημα UAC, το πρόγραμμα τερματίζει πράγματι και το αποτέλεσμα γράφεται στο `last-update.log`· μια αποτυχία εμφανίζεται στην επόμενη εκκίνηση.
- **Δύο αντίγραφα**: αν υπάρχει ακόμα παλιό αντίγραφο ανά χρήστη, το πρόγραμμα προτείνει να το αφαιρέσει· λάθος εγγραφή autostart διορθώνεται.
- **Εκκίνηση με τα Windows**: μία τιμή ανά χρήστη, γραμμένη από το ίδιο το πρόγραμμα· ο installer δεν γράφει πια τιμή σε HKLM.
- **Τα tests/probes δεν αγγίζουν πια το πραγματικό registry** (το audit ξαναέγραφε το autostart με τη διαδρομή του build υπό δοκιμή).
- **Καθαρή εγκατάσταση**: εφαρμόζονται πλέον η γλώσσα και η μπάρα που διάλεξε ο χρήστης στον installer.
- 24 νέα τεστ· μεταφράσεις 14 γλωσσών.

**Δεν έχει ελεγχθεί**: ο installer δεν εκτελέστηκε (μόνο μεταγλωττίστηκε)· η αφαίρεση πραγματικού αντιγράφου και το UAC χρειάζονται εικονική μηχανή.

## English

**What is fixed**
- **An update from 0.17.0 left two copies and did not update.** The setup removes the older per-user copy first and checks the result: it ends only the program of that exact folder, runs its uninstaller, verifies the entry and the folder are gone, cleans leftovers and stops with a message if anything remains.
- **Update**: the setup starts with an explicit UAC request, the program really ends and the result is written to `last-update.log`; a failure is shown at the next start.
- **Two copies**: if an older per-user copy is still there, the program offers to remove it; a wrong start-up entry is repaired.
- **Start with Windows**: one per-user value written by the program itself; the setup no longer writes an HKLM value.
- **Tests and probes no longer touch the real registry** (the audit rewrote the start-up value with the path of the build under test).
- **Clean installation**: the language and bar chosen in the setup are now applied.
- 24 new tests; 14 languages.

**Not verified**: the setup was not run (compile only); removing a real copy and the UAC path need a disposable machine.
