# Opsis News Crawler 0.12.8

> Το κύριο Opsis - Desktop Customization παραμένει στην έκδοση **1.9.8**. / The main application Opsis - Desktop Customization stays at version **1.9.8**.

## Ελληνικά
- **Μπάρα επιφάνειας όταν κρύβεται η γραμμή εργασιών**: με αυτόματη απόκρυψη, η εσωτερική μπάρα χάνεται μαζί με τη γραμμή και εμφανίζεται η μπάρα επιφάνειας εργασίας στη θέση και το μέγεθος που όρισε ο χρήστης· εξαφανίζεται όταν η γραμμή ξαναφανεί. Επιλογή στις Μπάρες (ενεργή από προεπιλογή, 14 γλώσσες). Δοκιμάστηκε με πραγματική αλλαγή της αυτόματης απόκρυψης των Windows.
- **Μετονομασία MotionDesk → Opsis** ολοκληρωμένη και στον φάκελο plugin: ο installer σβήνει τα παλιά `OpsisNewsCrawler.MotionDeskPlugin.*` και το script έκδοσης απορρίπτει αρχεία με το παλιό όνομα.
- Το Opsis 1.9.8 υιοθέτησε το `installer\dependencies.iss` και πρόσθεσε ενέργεια Uninstall για plugin· δεν χρειάστηκε αλλαγή εδώ. Το συμβόλαιο plugin μένει 0.8.0.0.
- Ισχύουν όλα τα νέα του 0.12.5 (κατηγορίες Άμυνα & Διπλωματία / Αθλητικά, χαρτοφυλάκιο, εξαρτήσεις, νέο εικονίδιο).

## English
- **Desktop bar while the taskbar is hidden**: with auto-hide, the bar inside the taskbar disappears with it and the desktop bar appears at the place and size the user set; it goes away when the taskbar returns. Option in Bars (on by default, 14 languages). Tested by really switching the Windows auto-hide on and off.
- **MotionDesk → Opsis rename** finished in the plugin folder too: the installer removes old `OpsisNewsCrawler.MotionDeskPlugin.*` files and the release script rejects old-named files.
- Opsis 1.9.8 adopted `installer\dependencies.iss` and added a plugin Uninstall action; nothing to change here. The plugin contract stays at 0.8.0.0.
- Everything new in 0.12.5 still applies (Defence & Diplomacy / Sports categories, portfolio, dependencies, new icon).

Not tried on a real system: the .NET/WebView2 download path of the installer, a bar on a second monitor, the weather-map window placement. Not code-signed: SmartScreen may warn.
