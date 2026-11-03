# Opsis News Crawler 0.19.0

## Ελληνικά
- **Επίπεδα απόδοσης (Χαμηλή / Ισορροπημένη / Υψηλή)**: το πρόγραμμα ανιχνεύει τον υπολογιστή (λογικοί επεξεργαστές, μνήμη, πραγματική κάρτα γραφικών, μπαταρία / εξοικονόμηση μπαταρίας) και προσαρμόζει βήμα κύλισης, έλεγχο γραμμής εργασιών, animations, εικονίδιο στην περιοχή ειδοποιήσεων και διαστήματα ανανέωσης. Ρυθμίσεις > Γενικά > *Απόδοση*: Αυτόματη / Χαμηλή / Ισορροπημένη / Υψηλή, με ένδειξη τι ανιχνεύθηκε και γιατί. Η υποβάθμιση είναι άμεση, η αναβάθμιση θέλει δύο ίδιες μετρήσεις (~2 λεπτά)· ένας φορητός χωρίς πρίζα πάει σε Ισορροπημένη και επιστρέφει μόνος του. Μέσα στο Opsis ισχύει το επίπεδο που δηλώνει το Opsis.
- **Έλεγχος του installer από άκρη σε άκρη σε καθαρά Windows (δωρεάν GitHub runners)**: workflow `installer-e2e` — εγκατάσταση της προηγούμενης έκδοσης (και της per-user 0.17.0 με το πρόγραμμα ανοιχτό), αναβάθμιση, έλεγχος registry / φακέλων / autostart / συντομεύσεων, εκκίνηση, audits με Χαμηλό και Υψηλό επίπεδο, απεγκατάσταση χωρίς υπολείμματα.
- Δεν τρέχει ακόμα το ίδιο το workflow (θέλει το πρώτο push).

**Δεν ελέγχεται σε runners**: UAC, SmartScreen, πραγματική κάρτα γραφικών, πολλές οθόνες, DPI ανά οθόνη. **Δεν έχει μετρηθεί**: CPU/RAM σε αδράνεια ανά επίπεδο.

## English
- **Performance tiers (Low / Balanced / High)**: the program detects the PC (logical processors, memory, a real graphics adapter, battery / battery saver) and adapts the scroll step, taskbar polling, animations, the tray icon and the refresh intervals. Settings > General > *Performance*: Automatic / Low / Balanced / High, showing what was detected and why. Down is immediate, up needs two equal readings (~2 min); an unplugged laptop goes to Balanced and returns by itself. Inside Opsis the tier Opsis reports applies.
- **Installer end-to-end test on clean Windows (free GitHub runners)**: workflow `installer-e2e` — installs the previous release (also per-user 0.17.0 with the program running), upgrades, checks registry / folders / start-up / shortcuts, starts the program, runs the audits with a Low and a High tier, uninstalls and checks for leftovers.
- The workflow itself has not run yet (it needs the first push).

**Not testable on runners**: UAC, SmartScreen, a real graphics card, several monitors, per-monitor DPI. **Not measured**: CPU/RAM at idle per tier.
