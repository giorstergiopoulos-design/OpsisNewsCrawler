# Opsis News Crawler 0.17.0

## Ελληνικά

**Τι νέο υπάρχει**
- **Κοινά controls**: κουμπιά, check boxes, sliders, λίστες και μενού προέρχονται πλέον από τη νέα κοινή βιβλιοθήκη **Opsis.UI** (`libs\Opsis.UI.dll`), που θα χρησιμοποιήσουν και το Opsis και το Opsis Appearance· τα δικά του αντίγραφα του Crawler αφαιρέθηκαν. Τα χρώματα έρχονται από tokens θέματος (`IOpsisTheme`).
- **Καλύτερα controls**: check box με «μικτή» κατάσταση, δακτύλιος εστίασης για το πληκτρολόγιο, κλιμάκωση κουτιού/τικ/γωνιών με το DPI της οθόνης και αντίστροφη διάταξη για γλώσσες δεξιά-προς-αριστερά· slider που παίρνει εστίαση με το κλικ, απαντά σε Home/End/PageUp/PageDown και ροδέλα (όταν έχει εστίαση) και ειδοποιεί μόνο όταν η τιμή αλλάζει πραγματικά· οι λίστες ανοίγουν με F4 / Alt+Κάτω / Space.
- 33 τεστ της βιβλιοθήκης και εικόνες κάθε control στο 100 / 150 / 200 % σε σκούρο και ανοιχτό θέμα.

**Δεν έχει ελεγχθεί**: έλεγχος σε πραγματική οθόνη 150 % (τα ίδια τα controls ελέγχθηκαν σε εικόνες με παράγοντα κλίμακας, η σελίδα ρυθμίσεων μόνο στο 100 %), ζωντανός έλεγχος μέσα στο Opsis.

## English

**What's new**
- **Shared controls**: buttons, check boxes, sliders, drop-down lists and menus now come from the new shared library **Opsis.UI** (`libs\Opsis.UI.dll`), which Opsis and Opsis Appearance will use too; the Crawler's own copies are gone. Colours come from theme tokens (`IOpsisTheme`).
- **Better controls**: check boxes with a mixed state, a keyboard focus ring, DPI scaling of box / tick / corners and right-to-left mirroring; sliders take focus on click, answer Home / End / PageUp / PageDown and the wheel (while focused) and raise the change event only on a real change; drop-downs open with F4 / Alt+Down / Space.
- 33 library tests and pictures of every control at 100 / 150 / 200 % in dark and light.

**Not verified**: a real 150 % monitor (the controls were checked in pictures with a scale factor, the settings window only at 100 %), a live check inside Opsis.
