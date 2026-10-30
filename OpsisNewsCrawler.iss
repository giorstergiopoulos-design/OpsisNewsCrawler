; Inno Setup 6/7 script for Opsis News Crawler. Saved as UTF-8 with BOM so the Greek texts display correctly.
; Run it through Run-OpsisNewsCrawler-Build.bat (option 2 or 3) after the "bin" folder has been created.
;
; Two ways to install (chosen on the second wizard page):
;   * Integrated: the crawler is installed as a plugin of Opsis - Desktop Customization, into
;     %AppData%\Opsis\plugins\Opsis - Desktop Customization (a folder Opsis scans at start-up). No separate app, no tray
;     icon, no shortcuts: its settings are the "News Crawler" page of Opsis.
;   * Separate: the stand-alone app (tray icon, own settings window, optional start with Windows).
; The wizard looks for Opsis (uninstall entry, default folders) and proposes the integrated install when it finds it.
; Test switches: /MODE=integrated | /MODE=separate (silent installs), /PLUGINROOT=<folder> (instead of %AppData%\Opsis\plugins).
#define MyAppName "Opsis News Crawler"
#define MyAppVersion "0.18.7"
#ifndef BinDir
#define BinDir "bin"
#endif
#define MyAppExe "OpsisNewsCrawler.exe"
#define OpsisAppId "A9E8F765-1234-4567-89AB-CDEF01234567"
#define PluginFolder "Opsis - Desktop Customization"

[Setup]
AppId={{B7E3C9A2-5D41-4F68-9A0E-3C1D7F52A8B4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Opsis
SetupIconFile=src\OpsisNewsCrawler.Windows\Assets\app.ico
; Wizard pictures drawn from the app artwork (BrandArt, --make-brand-art): 100 / 125 / 150 / 200 %
WizardImageFile=installer\wizard\large-100.bmp,installer\wizard\large-125.bmp,installer\wizard\large-150.bmp,installer\wizard\large-200.bmp
WizardSmallImageFile=installer\wizard\small-100.bmp,installer\wizard\small-125.bmp,installer\wizard\small-150.bmp,installer\wizard\small-200.bmp
DefaultDirName={code:GetDefaultDir}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=Output
OutputBaseFilename=OpsisNewsCrawler-Setup
Compression=lzma2
SolidCompression=yes
; Machine-wide: the program lives in Program Files (writable by administrators only), the settings stay per user in %AppData%\OpsisNewsCrawler.
; An earlier per-user installation (%LocalAppData%\Programs) is removed first, see RemovePerUserInstall.
PrivilegesRequired=admin
; The script reads and cleans the per-user (HKCU) entries of an older per-user installation on purpose (RemovePerUserInstall).
UsedUserAreasWarning=no
ArchitecturesInstallIn64BitMode=x64compatible
; Entry in Windows "Apps / Installed apps" with an Uninstall button
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExe}
Uninstallable=yes
CreateUninstallRegKey=yes
; The app creates this mutex, so setup and uninstall ask the user to close it first
AppMutex=OpsisNewsCrawler.SingleInstance
CloseApplications=yes
RestartApplications=no
; Greek Windows -> Greek wizard, anything else -> English, without asking
ShowLanguageDialog=auto
LanguageDetectionMethod=uilanguage

[Languages]
; Same fourteen languages as Opsis. The wizard follows the Windows display language; English is the fallback (first entry).
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "greek"; MessagesFile: "installer\Greek.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "chinese"; MessagesFile: "installer\Chinese.isl"
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"
Name: "hindi"; MessagesFile: "installer\Hindi.isl"

[CustomMessages]
english.DeleteSettings=Also delete the Opsis News Crawler settings (config.json)?
english.OldInstallRemains=An earlier per-user copy of Opsis News Crawler could not be removed (is it running?). Close it and run setup again.
english.StartWithWindows=Start with Windows
english.DesktopShortcut=Desktop shortcut
english.UninstallShortcut=Uninstall {#MyAppName}
english.LaunchApp=Launch {#MyAppName}
english.ModeTitle=Installation type
english.ModeSubtitle=How do you want to install {#MyAppName}?
english.ModeIntegrated=Integrate with Opsis - Desktop Customization (as a plugin)
english.ModeSeparate=Separate installation (stand-alone program)
english.OpsisFound=Opsis - Desktop Customization was found on this computer (version %1).
english.OpsisNotFound=Opsis - Desktop Customization was not found on this computer. You can install the crawler separately, or integrate it now and Opsis will pick it up as soon as it is installed.
english.ModeIntegratedHelp=Integrated: the crawler appears as the "News Crawler" page of Opsis, uses its colours and language, and has no separate tray icon or shortcuts.
english.ModeSeparateHelp=Separate: its own program with a tray icon, its own settings window and an optional start with Windows.
english.BarTitle=Default bar
english.BarSubtitle=Which bar should be shown by default?
english.BarAbove=The bar above the taskbar (touching the taskbar)
english.BarInside=The bar built into the taskbar (in the empty space after the apps) (experimental)
english.BarHelp=You can change this later in the settings, and you can show both bars at the same time (Bars tab).
english.RestartOpsis=Restart Opsis - Desktop Customization: the "News Crawler" page appears in its sidebar.
greek.DeleteSettings=Να διαγραφούν και οι ρυθμίσεις του Opsis News Crawler (config.json);
greek.OldInstallRemains=Δεν ήταν δυνατή η αφαίρεση προηγούμενου αντιγράφου του Opsis News Crawler (ανά χρήστη). Κλείστε το (τρέχει;) και ξαναεκτελέστε την εγκατάσταση.
greek.StartWithWindows=Εκκίνηση με τα Windows
greek.DesktopShortcut=Συντόμευση στην επιφάνεια εργασίας
greek.UninstallShortcut=Απεγκατάσταση {#MyAppName}
greek.LaunchApp=Εκκίνηση του {#MyAppName}
greek.ModeTitle=Τρόπος εγκατάστασης
greek.ModeSubtitle=Πώς θέλετε να εγκατασταθεί το {#MyAppName};
greek.ModeIntegrated=Ενσωμάτωση στο Opsis - Desktop Customization (ως plugin)
greek.ModeSeparate=Ξεχωριστή εγκατάσταση (αυτόνομο πρόγραμμα)
greek.OpsisFound=Βρέθηκε το Opsis - Desktop Customization σε αυτόν τον υπολογιστή (έκδοση %1).
greek.OpsisNotFound=Δεν βρέθηκε το Opsis - Desktop Customization σε αυτόν τον υπολογιστή. Μπορείτε να εγκαταστήσετε το crawler ξεχωριστά ή να το ενσωματώσετε τώρα, και το Opsis θα το εντοπίσει μόλις εγκατασταθεί.
greek.ModeIntegratedHelp=Ενσωματωμένο: το crawler εμφανίζεται ως η σελίδα «News Crawler» του Opsis, παίρνει τα χρώματα και τη γλώσσα του και δεν έχει δικό του εικονίδιο ούτε συντομεύσεις.
greek.ModeSeparateHelp=Ξεχωριστό: δικό του πρόγραμμα με εικονίδιο στην περιοχή ειδοποιήσεων, δικό του παράθυρο ρυθμίσεων και προαιρετική εκκίνηση με τα Windows.
greek.BarTitle=Προεπιλεγμένη μπάρα
greek.BarSubtitle=Ποια μπάρα να εμφανίζεται από προεπιλογή;
greek.BarAbove=Η μπάρα πάνω από τη γραμμή εργασιών (εφαπτόμενη σε αυτή)
greek.BarInside=Η μπάρα ενσωματωμένη στη γραμμή εργασιών (στον κενό χώρο μετά τις εφαρμογές) (πειραματικό)
greek.BarHelp=Μπορείτε να το αλλάξετε αργότερα στις ρυθμίσεις και να εμφανίζονται και οι δύο μπάρες μαζί (καρτέλα Μπάρες).
greek.RestartOpsis=Επανεκκινήστε το Opsis - Desktop Customization: η σελίδα «News Crawler» θα εμφανιστεί στο πλευρικό του μενού.
german.DeleteSettings=Auch die Einstellungen von Opsis News Crawler (config.json) löschen?
german.OldInstallRemains=Eine frühere benutzerbezogene Kopie von Opsis News Crawler konnte nicht entfernt werden (läuft sie noch?). Beenden Sie sie und starten Sie das Setup erneut.
german.StartWithWindows=Mit Windows starten
german.DesktopShortcut=Desktopverknüpfung
german.UninstallShortcut={#MyAppName} deinstallieren
german.LaunchApp={#MyAppName} starten
german.ModeTitle=Installationsart
german.ModeSubtitle=Wie möchten Sie {#MyAppName} installieren?
german.ModeIntegrated=In Opsis - Desktop Customization integrieren (als Plugin)
german.ModeSeparate=Separate Installation (eigenständiges Programm)
german.OpsisFound=Opsis - Desktop Customization wurde auf diesem Computer gefunden (Version %1).
german.OpsisNotFound=Opsis - Desktop Customization wurde auf diesem Computer nicht gefunden. Sie können den Crawler separat installieren oder jetzt integrieren; Opsis erkennt ihn, sobald es installiert ist.
german.ModeIntegratedHelp=Integriert: Der Crawler erscheint als Seite „News Crawler“ in Opsis, übernimmt dessen Farben und Sprache und hat kein eigenes Taskleistensymbol und keine Verknüpfungen.
german.ModeSeparateHelp=Separat: ein eigenes Programm mit Taskleistensymbol, eigenem Einstellungsfenster und optionalem Start mit Windows.
german.BarTitle=Standardleiste
german.BarSubtitle=Welche Leiste soll standardmäßig angezeigt werden?
german.BarAbove=Die Leiste über der Taskleiste (an der Taskleiste anliegend)
german.BarInside=Die in die Taskleiste eingebaute Leiste (im freien Platz nach den Apps) (experimentell)
german.BarHelp=Sie können dies später in den Einstellungen ändern und beide Leisten gleichzeitig anzeigen (Tab Leisten).
german.RestartOpsis=Starten Sie Opsis - Desktop Customization neu: Die Seite „News Crawler“ erscheint in der Seitenleiste.
french.DeleteSettings=Supprimer aussi les paramètres d'Opsis News Crawler (config.json) ?
french.OldInstallRemains=Une copie précédente d’Opsis News Crawler installée pour l’utilisateur n’a pas pu être supprimée (est-elle en cours d’exécution ?). Fermez-la et relancez l’installation.
french.StartWithWindows=Démarrer avec Windows
french.DesktopShortcut=Raccourci sur le Bureau
french.UninstallShortcut=Désinstaller {#MyAppName}
french.LaunchApp=Lancer {#MyAppName}
french.ModeTitle=Type d'installation
french.ModeSubtitle=Comment voulez-vous installer {#MyAppName} ?
french.ModeIntegrated=Intégrer à Opsis - Desktop Customization (comme plugin)
french.ModeSeparate=Installation séparée (programme autonome)
french.OpsisFound=Opsis - Desktop Customization a été trouvé sur cet ordinateur (version %1).
french.OpsisNotFound=Opsis - Desktop Customization est introuvable sur cet ordinateur. Vous pouvez installer le crawler séparément ou l'intégrer dès maintenant ; Opsis le détectera dès son installation.
french.ModeIntegratedHelp=Intégré : le crawler apparaît comme la page « News Crawler » d'Opsis, reprend ses couleurs et sa langue, et n'a ni icône de notification ni raccourcis propres.
french.ModeSeparateHelp=Séparé : un programme à part avec une icône de notification, sa propre fenêtre de paramètres et un démarrage facultatif avec Windows.
french.BarTitle=Barre par défaut
french.BarSubtitle=Quelle barre afficher par défaut ?
french.BarAbove=La barre au-dessus de la barre des tâches (contre elle)
french.BarInside=La barre intégrée à la barre des tâches (dans l'espace libre après les applications) (expérimental)
french.BarHelp=Vous pourrez le modifier plus tard dans les paramètres et afficher les deux barres en même temps (onglet Barres).
french.RestartOpsis=Redémarrez Opsis - Desktop Customization : la page « News Crawler » apparaît dans sa barre latérale.
spanish.DeleteSettings=¿Eliminar también la configuración de Opsis News Crawler (config.json)?
spanish.OldInstallRemains=No se pudo eliminar una copia anterior de Opsis News Crawler instalada por usuario (¿se está ejecutando?). Ciérrela y vuelva a ejecutar la instalación.
spanish.StartWithWindows=Iniciar con Windows
spanish.DesktopShortcut=Acceso directo en el escritorio
spanish.UninstallShortcut=Desinstalar {#MyAppName}
spanish.LaunchApp=Iniciar {#MyAppName}
spanish.ModeTitle=Tipo de instalación
spanish.ModeSubtitle=¿Cómo desea instalar {#MyAppName}?
spanish.ModeIntegrated=Integrar con Opsis - Desktop Customization (como plugin)
spanish.ModeSeparate=Instalación independiente (programa autónomo)
spanish.OpsisFound=Se encontró Opsis - Desktop Customization en este equipo (versión %1).
spanish.OpsisNotFound=No se encontró Opsis - Desktop Customization en este equipo. Puede instalar el crawler por separado o integrarlo ahora; Opsis lo detectará en cuanto se instale.
spanish.ModeIntegratedHelp=Integrado: el crawler aparece como la página «News Crawler» de Opsis, usa sus colores e idioma y no tiene icono de bandeja ni accesos directos propios.
spanish.ModeSeparateHelp=Independiente: un programa propio con icono de bandeja, su propia ventana de configuración e inicio opcional con Windows.
spanish.BarTitle=Barra predeterminada
spanish.BarSubtitle=¿Qué barra se debe mostrar de forma predeterminada?
spanish.BarAbove=La barra sobre la barra de tareas (pegada a ella)
spanish.BarInside=La barra integrada en la barra de tareas (en el espacio libre tras las aplicaciones) (experimental)
spanish.BarHelp=Puede cambiarlo más tarde en la configuración y mostrar ambas barras a la vez (pestaña Barras).
spanish.RestartOpsis=Reinicie Opsis - Desktop Customization: la página «News Crawler» aparecerá en su barra lateral.
korean.DeleteSettings=Opsis News Crawler 설정(config.json)도 삭제하시겠습니까?
korean.OldInstallRemains=이전의 사용자별 Opsis News Crawler를 제거하지 못했습니다(실행 중인가요?). 종료한 후 설치를 다시 실행하세요.
korean.StartWithWindows=Windows와 함께 시작
korean.DesktopShortcut=바탕 화면 바로 가기
korean.UninstallShortcut={#MyAppName} 제거
korean.LaunchApp={#MyAppName} 실행
korean.ModeTitle=설치 유형
korean.ModeSubtitle={#MyAppName}을(를) 어떻게 설치하시겠습니까?
korean.ModeIntegrated=Opsis - Desktop Customization에 통합 (플러그인으로)
korean.ModeSeparate=별도 설치 (독립 실행 프로그램)
korean.OpsisFound=이 컴퓨터에서 Opsis - Desktop Customization을(를) 찾았습니다(버전 %1).
korean.OpsisNotFound=이 컴퓨터에서 Opsis - Desktop Customization을(를) 찾을 수 없습니다. 크롤러를 별도로 설치하거나 지금 통합할 수 있으며, Opsis는 설치되는 즉시 이를 인식합니다.
korean.ModeIntegratedHelp=통합: 크롤러가 Opsis의 "News Crawler" 페이지로 표시되고 Opsis의 색과 언어를 사용하며, 별도의 트레이 아이콘이나 바로 가기가 없습니다.
korean.ModeSeparateHelp=별도: 트레이 아이콘, 자체 설정 창, 선택적 Windows 시작 기능이 있는 독립 프로그램입니다.
korean.BarTitle=기본 바
korean.BarSubtitle=기본으로 어떤 바를 표시할까요?
korean.BarAbove=작업 표시줄 위의 바 (작업 표시줄에 맞닿음)
korean.BarInside=작업 표시줄에 내장된 바 (앱 뒤의 빈 공간) (실험적)
korean.BarHelp=나중에 설정에서 변경할 수 있으며 두 바를 동시에 표시할 수도 있습니다(바 탭).
korean.RestartOpsis=Opsis - Desktop Customization을(를) 다시 시작하세요. 사이드바에 "News Crawler" 페이지가 나타납니다.
chinese.DeleteSettings=是否同时删除 Opsis News Crawler 的设置 (config.json)？
chinese.OldInstallRemains=无法删除之前按用户安装的 Opsis News Crawler（它是否正在运行？）。请先关闭它，然后重新运行安装程序。
chinese.StartWithWindows=随 Windows 启动
chinese.DesktopShortcut=桌面快捷方式
chinese.UninstallShortcut=卸载 {#MyAppName}
chinese.LaunchApp=启动 {#MyAppName}
chinese.ModeTitle=安装类型
chinese.ModeSubtitle=您想如何安装 {#MyAppName}？
chinese.ModeIntegrated=集成到 Opsis - Desktop Customization（作为插件）
chinese.ModeSeparate=单独安装（独立程序）
chinese.OpsisFound=在此计算机上找到了 Opsis - Desktop Customization（版本 %1）。
chinese.OpsisNotFound=在此计算机上未找到 Opsis - Desktop Customization。您可以单独安装 crawler，也可以现在集成；Opsis 安装后会自动识别。
chinese.ModeIntegratedHelp=集成：crawler 显示为 Opsis 的“News Crawler”页面，使用其颜色和语言，没有独立的托盘图标和快捷方式。
chinese.ModeSeparateHelp=单独：独立程序，带托盘图标、自己的设置窗口，并可选择随 Windows 启动。
chinese.BarTitle=默认栏
chinese.BarSubtitle=默认显示哪一栏？
chinese.BarAbove=任务栏上方的栏（紧贴任务栏）
chinese.BarInside=嵌入任务栏的栏（位于应用之后的空白区域） (实验性)
chinese.BarHelp=以后可在设置中更改，也可以同时显示两栏（“栏”选项卡）。
chinese.RestartOpsis=请重新启动 Opsis - Desktop Customization：侧边栏中会出现“News Crawler”页面。
italian.DeleteSettings=Eliminare anche le impostazioni di Opsis News Crawler (config.json)?
italian.OldInstallRemains=Non è stato possibile rimuovere una copia precedente di Opsis News Crawler installata per utente (è in esecuzione?). Chiudetela e riavviate l’installazione.
italian.StartWithWindows=Avvia con Windows
italian.DesktopShortcut=Collegamento sul desktop
italian.UninstallShortcut=Disinstalla {#MyAppName}
italian.LaunchApp=Avvia {#MyAppName}
italian.ModeTitle=Tipo di installazione
italian.ModeSubtitle=Come vuoi installare {#MyAppName}?
italian.ModeIntegrated=Integra in Opsis - Desktop Customization (come plugin)
italian.ModeSeparate=Installazione separata (programma autonomo)
italian.OpsisFound=Opsis - Desktop Customization è stato trovato su questo computer (versione %1).
italian.OpsisNotFound=Opsis - Desktop Customization non è stato trovato su questo computer. Puoi installare il crawler separatamente o integrarlo ora; Opsis lo rileverà appena installato.
italian.ModeIntegratedHelp=Integrato: il crawler appare come pagina «News Crawler» di Opsis, usa i suoi colori e la sua lingua e non ha icona nell'area di notifica né collegamenti propri.
italian.ModeSeparateHelp=Separato: un programma a sé con icona nell'area di notifica, finestra impostazioni propria e avvio facoltativo con Windows.
italian.BarTitle=Barra predefinita
italian.BarSubtitle=Quale barra mostrare per impostazione predefinita?
italian.BarAbove=La barra sopra la barra delle applicazioni (a contatto)
italian.BarInside=La barra integrata nella barra delle applicazioni (nello spazio libero dopo le app) (sperimentale)
italian.BarHelp=Puoi cambiarlo più tardi nelle impostazioni e mostrare entrambe le barre insieme (scheda Barre).
italian.RestartOpsis=Riavvia Opsis - Desktop Customization: la pagina «News Crawler» comparirà nella barra laterale.
russian.DeleteSettings=Удалить также настройки Opsis News Crawler (config.json)?
russian.OldInstallRemains=Не удалось удалить прежнюю копию Opsis News Crawler, установленную для пользователя (она запущена?). Закройте её и запустите установку снова.
russian.StartWithWindows=Запускать вместе с Windows
russian.DesktopShortcut=Ярлык на рабочем столе
russian.UninstallShortcut=Удалить {#MyAppName}
russian.LaunchApp=Запустить {#MyAppName}
russian.ModeTitle=Тип установки
russian.ModeSubtitle=Как установить {#MyAppName}?
russian.ModeIntegrated=Встроить в Opsis - Desktop Customization (как плагин)
russian.ModeSeparate=Отдельная установка (самостоятельная программа)
russian.OpsisFound=На этом компьютере найден Opsis - Desktop Customization (версия %1).
russian.OpsisNotFound=Opsis - Desktop Customization на этом компьютере не найден. Вы можете установить краулер отдельно или встроить его сейчас; Opsis обнаружит его, как только будет установлен.
russian.ModeIntegratedHelp=Встроенный: краулер появляется как страница «News Crawler» в Opsis, использует его цвета и язык и не имеет собственного значка в трее и ярлыков.
russian.ModeSeparateHelp=Отдельный: самостоятельная программа со значком в трее, собственным окном настроек и необязательным запуском вместе с Windows.
russian.BarTitle=Панель по умолчанию
russian.BarSubtitle=Какую панель показывать по умолчанию?
russian.BarAbove=Панель над панелью задач (вплотную к ней)
russian.BarInside=Панель, встроенная в панель задач (в свободном месте после приложений) (экспериментально)
russian.BarHelp=Позже это можно изменить в настройках, а также показывать обе панели одновременно (вкладка «Панели»).
russian.RestartOpsis=Перезапустите Opsis - Desktop Customization: страница «News Crawler» появится в его боковой панели.
japanese.DeleteSettings=Opsis News Crawler の設定 (config.json) も削除しますか?
japanese.OldInstallRemains=以前にユーザー単位でインストールされた Opsis News Crawler を削除できませんでした（実行中ではありませんか？）。終了してからセットアップをもう一度実行してください。
japanese.StartWithWindows=Windows と同時に起動
japanese.DesktopShortcut=デスクトップにショートカットを作成
japanese.UninstallShortcut={#MyAppName} をアンインストール
japanese.LaunchApp={#MyAppName} を起動
japanese.ModeTitle=インストールの種類
japanese.ModeSubtitle={#MyAppName} をどのようにインストールしますか?
japanese.ModeIntegrated=Opsis - Desktop Customization に統合 (プラグインとして)
japanese.ModeSeparate=個別にインストール (単独のプログラム)
japanese.OpsisFound=このコンピューターで Opsis - Desktop Customization が見つかりました (バージョン %1)。
japanese.OpsisNotFound=このコンピューターで Opsis - Desktop Customization が見つかりません。クローラーを個別にインストールするか、今すぐ統合できます。Opsis はインストールされ次第、自動で認識します。
japanese.ModeIntegratedHelp=統合: クローラーは Opsis の「News Crawler」ページとして表示され、Opsis の色と言語を使い、専用のトレイアイコンやショートカットはありません。
japanese.ModeSeparateHelp=個別: トレイアイコン、専用の設定ウィンドウ、任意の Windows 同時起動を備えた独立したプログラムです。
japanese.BarTitle=既定のバー
japanese.BarSubtitle=既定でどのバーを表示しますか?
japanese.BarAbove=タスクバーの上のバー (タスクバーに接する)
japanese.BarInside=タスクバーに組み込まれたバー (アプリの後ろの空きスペース) (試験的)
japanese.BarHelp=後から設定で変更でき、両方のバーを同時に表示することもできます ([バー] タブ)。
japanese.RestartOpsis=Opsis - Desktop Customization を再起動してください。サイドバーに「News Crawler」ページが表示されます。
portuguese.DeleteSettings=Eliminar também as definições do Opsis News Crawler (config.json)?
portuguese.OldInstallRemains=Não foi possível remover uma cópia anterior do Opsis News Crawler instalada por utilizador (está em execução?). Feche-a e execute a instalação novamente.
portuguese.StartWithWindows=Iniciar com o Windows
portuguese.DesktopShortcut=Atalho no ambiente de trabalho
portuguese.UninstallShortcut=Desinstalar {#MyAppName}
portuguese.LaunchApp=Iniciar {#MyAppName}
portuguese.ModeTitle=Tipo de instalação
portuguese.ModeSubtitle=Como pretende instalar o {#MyAppName}?
portuguese.ModeIntegrated=Integrar no Opsis - Desktop Customization (como plugin)
portuguese.ModeSeparate=Instalação separada (programa autónomo)
portuguese.OpsisFound=O Opsis - Desktop Customization foi encontrado neste computador (versão %1).
portuguese.OpsisNotFound=O Opsis - Desktop Customization não foi encontrado neste computador. Pode instalar o crawler em separado ou integrá-lo agora; o Opsis deteta-o assim que for instalado.
portuguese.ModeIntegratedHelp=Integrado: o crawler aparece como a página «News Crawler» do Opsis, usa as suas cores e idioma e não tem ícone na área de notificação nem atalhos próprios.
portuguese.ModeSeparateHelp=Separado: um programa próprio com ícone na área de notificação, janela de definições própria e arranque opcional com o Windows.
portuguese.BarTitle=Barra predefinida
portuguese.BarSubtitle=Que barra deve ser mostrada por predefinição?
portuguese.BarAbove=A barra acima da barra de tarefas (encostada)
portuguese.BarInside=A barra integrada na barra de tarefas (no espaço livre depois das aplicações) (experimental)
portuguese.BarHelp=Pode alterá-lo mais tarde nas definições e mostrar ambas as barras em simultâneo (separador Barras).
portuguese.RestartOpsis=Reinicie o Opsis - Desktop Customization: a página «News Crawler» aparece na barra lateral.
turkish.DeleteSettings=Opsis News Crawler ayarları (config.json) da silinsin mi?
turkish.OldInstallRemains=Opsis News Crawler’ın önceki kullanıcı başına kopyası kaldırılamadı (çalışıyor olabilir mi?). Kapatın ve kurulumu yeniden çalıştırın.
turkish.StartWithWindows=Windows ile başlat
turkish.DesktopShortcut=Masaüstü kısayolu
turkish.UninstallShortcut={#MyAppName} programını kaldır
turkish.LaunchApp={#MyAppName} programını başlat
turkish.ModeTitle=Kurulum türü
turkish.ModeSubtitle={#MyAppName} nasıl kurulsun?
turkish.ModeIntegrated=Opsis - Desktop Customization ile bütünleştir (eklenti olarak)
turkish.ModeSeparate=Ayrı kurulum (bağımsız program)
turkish.OpsisFound=Opsis - Desktop Customization bu bilgisayarda bulundu (sürüm %1).
turkish.OpsisNotFound=Opsis - Desktop Customization bu bilgisayarda bulunamadı. Crawler'ı ayrı kurabilir veya şimdi bütünleştirebilirsiniz; Opsis kurulur kurulmaz onu algılar.
turkish.ModeIntegratedHelp=Bütünleşik: crawler, Opsis'in “News Crawler” sayfası olarak görünür, onun renklerini ve dilini kullanır; ayrı bildirim simgesi ve kısayolu yoktur.
turkish.ModeSeparateHelp=Ayrı: bildirim alanı simgesi, kendi ayarlar penceresi ve isteğe bağlı Windows ile başlatma olan ayrı bir program.
turkish.BarTitle=Varsayılan çubuk
turkish.BarSubtitle=Varsayılan olarak hangi çubuk gösterilsin?
turkish.BarAbove=Görev çubuğunun üstündeki çubuk (ona bitişik)
turkish.BarInside=Görev çubuğuna yerleşik çubuk (uygulamalardan sonraki boş alanda) (deneysel)
turkish.BarHelp=Bunu daha sonra ayarlardan değiştirebilir ve iki çubuğu aynı anda gösterebilirsiniz (Çubuklar sekmesi).
turkish.RestartOpsis=Opsis - Desktop Customization'ı yeniden başlatın: “News Crawler” sayfası kenar çubuğunda görünür.
arabic.DeleteSettings=هل تريد حذف إعدادات Opsis News Crawler (config.json) أيضًا؟
arabic.OldInstallRemains=تعذّرت إزالة نسخة سابقة من Opsis News Crawler مثبّتة لكل مستخدم (هل هي قيد التشغيل؟). أغلقها ثم شغّل التثبيت مرة أخرى.
arabic.StartWithWindows=التشغيل مع Windows
arabic.DesktopShortcut=اختصار على سطح المكتب
arabic.UninstallShortcut=إلغاء تثبيت {#MyAppName}
arabic.LaunchApp=تشغيل {#MyAppName}
arabic.ModeTitle=نوع التثبيت
arabic.ModeSubtitle=كيف تريد تثبيت {#MyAppName}؟
arabic.ModeIntegrated=الدمج مع Opsis - Desktop Customization (كإضافة)
arabic.ModeSeparate=تثبيت منفصل (برنامج مستقل)
arabic.OpsisFound=تم العثور على Opsis - Desktop Customization على هذا الكمبيوتر (الإصدار %1).
arabic.OpsisNotFound=لم يتم العثور على Opsis - Desktop Customization على هذا الكمبيوتر. يمكنك تثبيت crawler بشكل منفصل أو دمجه الآن، وسيتعرف عليه Opsis بمجرد تثبيته.
arabic.ModeIntegratedHelp=مدمج: يظهر crawler كصفحة «News Crawler» في Opsis، ويستخدم ألوانه ولغته، وليس له أيقونة منطقة إشعارات أو اختصارات خاصة.
arabic.ModeSeparateHelp=منفصل: برنامج مستقل بأيقونة في منطقة الإشعارات ونافذة إعدادات خاصة وتشغيل اختياري مع Windows.
arabic.BarTitle=الشريط الافتراضي
arabic.BarSubtitle=أي شريط تريد عرضه افتراضيًا؟
arabic.BarAbove=الشريط فوق شريط المهام (ملاصق له)
arabic.BarInside=الشريط المدمج في شريط المهام (في المساحة الفارغة بعد التطبيقات) (تجريبي)
arabic.BarHelp=يمكنك تغيير ذلك لاحقًا من الإعدادات وعرض الشريطين معًا (علامة التبويب الأشرطة).
arabic.RestartOpsis=أعد تشغيل Opsis - Desktop Customization: ستظهر صفحة «News Crawler» في الشريط الجانبي.
hindi.DeleteSettings=क्या Opsis News Crawler की सेटिंग्स (config.json) भी हटानी हैं?
hindi.OldInstallRemains=Opsis News Crawler की पिछली प्रति-उपयोगकर्ता कॉपी हटाई नहीं जा सकी (क्या वह चल रही है?). उसे बंद करें और सेटअप फिर चलाएँ।
hindi.StartWithWindows=Windows के साथ शुरू करें
hindi.DesktopShortcut=डेस्कटॉप शॉर्टकट
hindi.UninstallShortcut={#MyAppName} अनइंस्टॉल करें
hindi.LaunchApp={#MyAppName} चलाएँ
hindi.ModeTitle=इंस्टॉलेशन का प्रकार
hindi.ModeSubtitle=आप {#MyAppName} को कैसे इंस्टॉल करना चाहते हैं?
hindi.ModeIntegrated=Opsis - Desktop Customization में जोड़ें (प्लगइन के रूप में)
hindi.ModeSeparate=अलग इंस्टॉलेशन (स्वतंत्र प्रोग्राम)
hindi.OpsisFound=इस कंप्यूटर पर Opsis - Desktop Customization मिला (संस्करण %1)।
hindi.OpsisNotFound=इस कंप्यूटर पर Opsis - Desktop Customization नहीं मिला। आप crawler को अलग से इंस्टॉल कर सकते हैं या अभी जोड़ सकते हैं; Opsis इंस्टॉल होते ही उसे पहचान लेगा।
hindi.ModeIntegratedHelp=जुड़ा हुआ: crawler, Opsis के "News Crawler" पेज के रूप में दिखता है, उसके रंग और भाषा इस्तेमाल करता है और उसका अपना ट्रे आइकन या शॉर्टकट नहीं होता।
hindi.ModeSeparateHelp=अलग: अपना ट्रे आइकन, अपनी सेटिंग्स विंडो और Windows के साथ वैकल्पिक शुरुआत वाला स्वतंत्र प्रोग्राम।
hindi.BarTitle=डिफ़ॉल्ट पट्टी
hindi.BarSubtitle=डिफ़ॉल्ट रूप से कौन-सी पट्टी दिखे?
hindi.BarAbove=टास्कबार के ऊपर की पट्टी (उससे सटी हुई)
hindi.BarInside=टास्कबार में जुड़ी पट्टी (ऐप्स के बाद की खाली जगह में) (प्रायोगिक)
hindi.BarHelp=आप इसे बाद में सेटिंग्स में बदल सकते हैं और दोनों पट्टियाँ एक साथ दिखा सकते हैं (पट्टियाँ टैब)।
hindi.RestartOpsis=Opsis - Desktop Customization को फिर से शुरू करें: साइडबार में "News Crawler" पेज दिखाई देगा।

[Tasks]
Name: "startup"; Description: "{cm:StartWithWindows}"; Flags: unchecked; Check: IsSeparate
Name: "desktopicon"; Description: "{cm:DesktopShortcut}"; Flags: unchecked; Check: IsSeparate

[Files]
; Separate install: the program (the plugin copy stays out, Opsis must not load a second crawler next to the stand-alone one)
Source: "{#BinDir}\*"; DestDir: "{app}"; Excludes: "plugins";Flags: recursesubdirs createallsubdirs ignoreversion; Check: IsSeparate
; Integrated install: only the plugin folder, into the folder Opsis scans ({app} is set to it on the wizard page)
Source: "{#BinDir}\plugins\{#PluginFolder}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion; Check: IsIntegrated

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Check: IsSeparate
Name: "{group}\{cm:UninstallShortcut}"; Filename: "{uninstallexe}"; Check: IsSeparate
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon; Check: IsSeparate

[InstallDelete]
; The plugin DLL was renamed (MotionDesk -> Opsis) in 0.12.5: remove the old copy so Opsis does not find two plugin DLLs in one folder.
Type: files; Name: "{app}\OpsisNewsCrawler.MotionDeskPlugin.*"

[Registry]
; Start with Windows (removed again by the uninstaller)
; "Start with Windows" is the user's own per-user Run value, written by the program itself at its first start (the task only carries the wish in
; first-run.json). The machine-wide value that 0.18.0 / 0.18.5 wrote is removed, so there is never a second start-up entry.
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "{#MyAppName}"; Flags: deletevalue
; Clean up the start-up entry of the very first versions (they were called DeskTicker)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "DeskTicker"; Flags: deletevalue
; Clean up the plugin-path entry written by an earlier build of this installer
Root: HKCU; Subkey: "Software\Opsis\PluginPaths"; ValueName: "OpsisNewsCrawler"; Flags: deletevalue

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent; Check: IsSeparate

; Dependencies page (.NET Desktop Runtime, WebView2 Runtime): see installerdependencies.iss
#include "installer\dependencies.iss"

[Code]
var
  BarPage: TInputOptionWizardPage;
  ModePage: TInputOptionWizardPage;
  OpsisVersion: String;
  OpsisFound: Boolean;
  InfoAdded: Boolean;

// ---- finding Opsis -------------------------------------------------------------------------

function ReadOpsisVersion(RootKey: Integer; var Version: String): Boolean;
var
  Key: String;
begin
  Key := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{{#OpsisAppId}}_is1';
  Result := RegQueryStringValue(RootKey, Key, 'DisplayVersion', Version);
  if not Result then
    Result := RegKeyExists(RootKey, Key);
end;

function DetectOpsis(): Boolean;
begin
  OpsisVersion := '';
  // The installer of Opsis registers itself for all users (64-bit or 32-bit view) or for the current user.
  Result := ReadOpsisVersion(HKLM64, OpsisVersion) or ReadOpsisVersion(HKLM32, OpsisVersion) or ReadOpsisVersion(HKCU, OpsisVersion);
  if not Result then
    Result := FileExists(ExpandConstant('{autopf}\Opsis - Desktop Customization\Opsis.exe')) or
              FileExists(ExpandConstant('{localappdata}\Programs\Opsis - Desktop Customization\Opsis.exe'));
  if Result and (OpsisVersion = '') then OpsisVersion := '?';
end;

// ---- the two modes -------------------------------------------------------------------------

function IsIntegrated(): Boolean;
begin
  if WizardSilent or (ModePage = nil) then
    Result := CompareText(ExpandConstant('{param:MODE|separate}'), 'integrated') = 0
  else
    Result := ModePage.Values[0];
end;

function IsSeparate(): Boolean;
begin
  Result := not IsIntegrated();
end;

// Where Opsis is installed (empty when unknown): its uninstall entry, machine-wide first.
function OpsisInstallDir(): String;
var
  Key, Dir: String;
begin
  Result := '';
  Key := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{{#OpsisAppId}}_is1';
  if RegQueryStringValue(HKLM64, Key, 'InstallLocation', Dir) or RegQueryStringValue(HKLM32, Key, 'InstallLocation', Dir) then
    Result := RemoveBackslashUnlessRoot(Dir);
  if (Result = '') and FileExists(ExpandConstant('{autopf}\Opsis - Desktop Customization\Opsis.exe')) then
    Result := ExpandConstant('{autopf}\Opsis - Desktop Customization');
end;

// The plugin folder. Opsis loads plugins without asking only from a protected folder: <Opsis folder>\plugins when Opsis is in Program Files.
// Otherwise the per-user folder is used, and Opsis asks the user once to approve the exact files.
function PluginDir(): String;
var
  Root, Opsis: String;
begin
  Root := ExpandConstant('{param:PLUGINROOT|}');
  if Root = '' then
  begin
    Opsis := OpsisInstallDir();
    if (Opsis <> '') and (Pos(Lowercase(ExpandConstant('{autopf}')), Lowercase(Opsis)) = 1) then
      Root := AddBackslash(Opsis) + 'plugins'
    else
      Root := ExpandConstant('{userappdata}\Opsis\plugins');
  end;
  Result := AddBackslash(Root) + '{#PluginFolder}';
end;

// The default folder: the plugin folder of Opsis for a silent integrated install, the usual program folder otherwise.
function GetDefaultDir(Param: String): String;
begin
  if CompareText(ExpandConstant('{param:MODE|separate}'), 'integrated') = 0 then
    Result := PluginDir()
  else
    Result := ExpandConstant('{autopf}\{#MyAppName}');
end;

procedure InitializeWizard();
begin
  OpsisFound := DetectOpsis();
  ModePage := CreateInputOptionPage(wpWelcome, CustomMessage('ModeTitle'), CustomMessage('ModeSubtitle'),
    '', True, False);
  ModePage.Add(CustomMessage('ModeIntegrated'));
  ModePage.Add(CustomMessage('ModeSeparate'));
  // Opsis found: propose the integration; not found: propose the separate installation.
  if OpsisFound then
    ModePage.SelectedValueIndex := 0
  else
    ModePage.SelectedValueIndex := 1;

  // Which bar is the default one.
  BarPage := CreateInputOptionPage(ModePage.ID, CustomMessage('BarTitle'), CustomMessage('BarSubtitle'), CustomMessage('BarHelp'), True, False);
  BarPage.Add(CustomMessage('BarAbove'));
  BarPage.Add(CustomMessage('BarInside'));
  BarPage.SelectedValueIndex := 0;

  // Which required components to download and install (.NET Desktop Runtime, WebView2).
  DepsInit(BarPage.ID);
end;

// The bar chosen as the default: /BAR=above|taskbar for silent installs.
function ChosenBar(): String;
begin
  if WizardSilent or (BarPage = nil) then
  begin
    if CompareText(ExpandConstant('{param:BAR|above}'), 'taskbar') = 0 then Result := 'TaskbarIntegrated' else Result := 'BottomTaskbar';
  end
  else if BarPage.SelectedValueIndex = 1 then
    Result := 'TaskbarIntegrated'
  else
    Result := 'BottomTaskbar';
end;

// ISO code of the wizard language: the app starts in the language the user installed it in
function AppLanguage(): String;
var
  N: String;
begin
  N := ActiveLanguage;
  if N = 'greek' then Result := 'el'
  else if N = 'german' then Result := 'de'
  else if N = 'french' then Result := 'fr'
  else if N = 'spanish' then Result := 'es'
  else if N = 'korean' then Result := 'ko'
  else if N = 'chinese' then Result := 'zh'
  else if N = 'italian' then Result := 'it'
  else if N = 'russian' then Result := 'ru'
  else if N = 'japanese' then Result := 'ja'
  else if N = 'portuguese' then Result := 'pt'
  else if N = 'turkish' then Result := 'tr'
  else if N = 'arabic' then Result := 'ar'
  else if N = 'hindi' then Result := 'hi'
  else Result := 'en';
end;

// Earlier builds were installed per user (%LocalAppData%\Programs\Opsis News Crawler, uninstall entry and start-up entry in HKCU).
// Setup installs machine-wide, so the per-user copy is removed FIRST and the result is checked:
//   1. its running program is ended (only an image inside that exact folder, never anything else),
//   2. its own uninstaller runs silently (the settings in %AppData%\OpsisNewsCrawler are kept),
//   3. the uninstall entry and the folder must be gone (a few retries); what is left of OUR OWN old copy is cleaned by hand,
//   4. its start-up value (HKCU Run) is removed; the wish "start with Windows" is carried over to the new installation.
// If something is still there, setup stops with a message instead of leaving two copies.
const
  OldUninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{{B7E3C9A2-5D41-4F68-9A0E-3C1D7F52A8B4}_is1';
  RunKeyPath = 'Software\Microsoft\Windows\CurrentVersion\Run';

var
  HadAutostart: Boolean;

function PerUserCopyThere(Dir: String): Boolean;
begin
  Result := RegKeyExists(HKCU, OldUninstallKey) or ((Dir <> '') and DirExists(Dir));
end;

function RunValueIsMine(Root: Integer; Name: String): Boolean;
var
  Value: String;
begin
  Result := RegQueryStringValue(Root, RunKeyPath, Name, Value) and (Value <> '');
end;

// Ends the programs whose image lies inside the old per-user folder (exact path, so a program in Program Files or a build folder is never touched).
procedure EndProgramsIn(Dir: String);
var
  ResultCode: Integer;
  Quoted, Params: String;
begin
  Quoted := Dir;
  StringChangeEx(Quoted, '''', '''''', True);
  Params := '-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "Get-Process OpsisNewsCrawler -ErrorAction SilentlyContinue | ' +
            'Where-Object { $_.Path -and $_.Path.StartsWith(''' + Quoted + '\'', ''OrdinalIgnoreCase'') } | Stop-Process -Force"';
  Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

// Returns '' when the per-user copy is gone, otherwise the reason.
function RemovePerUserInstall: String;
var
  Cmd, Uninstaller, Dir, Programs: String;
  ResultCode, Attempt: Integer;
begin
  Result := '';
  HadAutostart := RunValueIsMine(HKCU, '{#MyAppName}') or RunValueIsMine(HKLM, '{#MyAppName}');
  Dir := '';
  Uninstaller := '';
  if RegQueryStringValue(HKCU, OldUninstallKey, 'InstallLocation', Dir) then Dir := RemoveBackslashUnlessRoot(Dir);
  if RegQueryStringValue(HKCU, OldUninstallKey, 'UninstallString', Cmd) then
  begin
    Uninstaller := RemoveQuotes(Cmd);
    if Dir = '' then Dir := ExtractFileDir(Uninstaller);
  end;

  Programs := Lowercase(ExpandConstant('{localappdata}\Programs\'));
  if (Dir <> '') and (Pos(Programs, Lowercase(AddBackslash(Dir))) = 1) then
  begin
    EndProgramsIn(Dir);
    // the uninstaller must lie in that folder: a value of the registry is never run from anywhere else
    if (Uninstaller <> '') and FileExists(Uninstaller) and (Pos(Lowercase(AddBackslash(Dir)), Lowercase(Uninstaller)) = 1) then
      Exec(Uninstaller, '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Attempt := 0;
    while PerUserCopyThere(Dir) and (Attempt < 5) do
    begin
      Sleep(1000);
      Attempt := Attempt + 1;
    end;
    if PerUserCopyThere(Dir) then
    begin
      // what is left of our own old copy: the uninstall entry and the folder (settings in %AppData% are not touched)
      RegDeleteKeyIncludingSubkeys(HKCU, OldUninstallKey);
      if DirExists(Dir) then DelTree(Dir, True, True, True);
    end;
    if PerUserCopyThere(Dir) then Result := Dir;
  end
  else if RegKeyExists(HKCU, OldUninstallKey) then
  begin
    // an uninstall entry of ours outside %LocalAppData%\Programs: not touched, but reported (never leave two copies silently)
    Result := OldUninstallKey;
  end;
  RegDeleteValue(HKCU, RunKeyPath, '{#MyAppName}');
  RegDeleteValue(HKCU, RunKeyPath, 'DeskTicker');
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Left: String;
begin
  Result := '';
  Left := RemovePerUserInstall;
  if Left <> '' then
    Result := CustomMessage('OldInstallRemains') + #13#10 + Left;
end;

procedure CurPageChanged_StartupDefault(CurPageID: Integer);
begin
  // an earlier copy started with Windows: the same wish is pre-selected for the new one
  if (CurPageID = wpSelectTasks) and HadAutostart then
    WizardSelectTasks('startup');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Dir, Json: String;
  Wish: Boolean;
begin
  if CurStep = ssInstall then
  begin
    if WizardSilent then DepsDownloadSelected();
    DepsInstallSelected();
  end;
  if CurStep = ssPostInstall then
  begin
    // read once by the app at its next start (AppConfig.ApplyFirstRun). A fresh installation carries the choices of the wizard; an update
    // (a config.json exists) never overwrites the user's language and bar, it only carries the "start with Windows" wish over.
    Dir := ExpandConstant('{userappdata}\OpsisNewsCrawler');
    ForceDirectories(Dir);
    Wish := IsSeparate() and (WizardIsTaskSelected('startup') or (WizardSilent and HadAutostart));
    Json := '';
    if not FileExists(Dir + '\config.json') then
    begin
      Json := '{"Position":"' + ChosenBar() + '","Language":"' + AppLanguage() + '"';
      if Wish then Json := Json + ',"StartWithWindows":true';
      Json := Json + '}';
    end
    else if Wish then
      Json := '{"StartWithWindows":true}';
    if Json <> '' then SaveStringToFile(Dir + '\first-run.json', Json, False);
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
var
  Info: TNewStaticText;
begin
  CurPageChanged_StartupDefault(CurPageID);
  if (ModePage <> nil) and (CurPageID = ModePage.ID) and (not InfoAdded) then
  begin
    // explanation texts under the two choices (added once)
    InfoAdded := True;
    Info := TNewStaticText.Create(ModePage);
    Info.Parent := ModePage.Surface;
    Info.AutoSize := False;
    Info.WordWrap := True;
    Info.Left := 0;
    Info.Width := ModePage.SurfaceWidth;
    Info.Top := ModePage.CheckListBox.Top + ModePage.CheckListBox.Height + ScaleY(10);
    Info.Height := ScaleY(150);
    if OpsisFound then
      Info.Caption := FmtMessage(CustomMessage('OpsisFound'), [OpsisVersion]) + #13#10#13#10
    else
      Info.Caption := CustomMessage('OpsisNotFound') + #13#10#13#10;
    Info.Caption := Info.Caption + CustomMessage('ModeIntegratedHelp') + #13#10#13#10 + CustomMessage('ModeSeparateHelp');
    ModePage.CheckListBox.Height := ScaleY(60);
    Info.Top := ModePage.CheckListBox.Top + ModePage.CheckListBox.Height + ScaleY(10);
  end;

  if CurPageID = wpFinished then
  begin
    if IsIntegrated() then
      WizardForm.FinishedLabel.Caption := WizardForm.FinishedLabel.Caption + #13#10#13#10 + CustomMessage('RestartOpsis');
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = wpReady) and not WizardSilent then Result := DepsDownloadSelected();
  if (ModePage <> nil) and (CurPageID = ModePage.ID) and (not WizardSilent) then
  begin
    // The "installation folder" is the plugin folder of Opsis when integrating (a silent install keeps /DIR as given).
    if IsIntegrated() then
      WizardForm.DirEdit.Text := PluginDir()
    else
      WizardForm.DirEdit.Text := ExpandConstant('{autopf}\{#MyAppName}');
  end;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  // Nothing to choose about folders and shortcuts when integrating.
  if IsIntegrated() and ((PageID = wpSelectDir) or (PageID = wpSelectProgramGroup) or (PageID = wpSelectTasks)) then
    Result := True;
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
begin
  if IsIntegrated() then
    Result := CustomMessage('ModeIntegrated') + NewLine + Space + PluginDir()
  else
    Result := MemoDirInfo + NewLine + NewLine + MemoGroupInfo + NewLine + NewLine + MemoTasksInfo;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{userappdata}\OpsisNewsCrawler');
    // Settings are only removed when the user says yes (never in a silent uninstall)
    if DirExists(DataDir) and (not UninstallSilent) then
      if MsgBox(CustomMessage('DeleteSettings'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(DataDir, True, True, True);
  end;
end;
