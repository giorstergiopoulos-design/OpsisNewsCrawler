; Inno Setup 6 script for Opsis News Crawler. Saved as UTF-8 with BOM so the Greek texts display correctly.
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
#define MyAppVersion "0.8.0"
#define MyAppExe "OpsisNewsCrawler.exe"
#define OpsisAppId "A9E8F765-1234-4567-89AB-CDEF01234567"
#define PluginFolder "Opsis - Desktop Customization"

[Setup]
AppId={{B7E3C9A2-5D41-4F68-9A0E-3C1D7F52A8B4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Opsis
SetupIconFile=src\OpsisNewsCrawler.Windows\Assets\app.ico
DefaultDirName={code:GetDefaultDir}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=Output
OutputBaseFilename=OpsisNewsCrawler-Setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=lowest
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
; The wizard shows ONE language: Greek on a Greek Windows, English otherwise (English is the fallback / first entry).
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "greek"; MessagesFile: "compiler:Languages\Greek.isl"

[CustomMessages]
english.DeleteSettings=Also delete the Opsis News Crawler settings (config.json)?
greek.DeleteSettings=Να διαγραφούν και οι ρυθμίσεις του Opsis News Crawler (config.json);
english.StartWithWindows=Start with Windows
greek.StartWithWindows=Εκκίνηση με τα Windows
english.DesktopShortcut=Desktop shortcut
greek.DesktopShortcut=Συντόμευση στην επιφάνεια εργασίας
english.UninstallShortcut=Uninstall {#MyAppName}
greek.UninstallShortcut=Απεγκατάσταση {#MyAppName}
english.LaunchApp=Launch {#MyAppName}
greek.LaunchApp=Εκκίνηση του {#MyAppName}
english.ModeTitle=Installation type
greek.ModeTitle=Τρόπος εγκατάστασης
english.ModeSubtitle=How do you want to install {#MyAppName}?
greek.ModeSubtitle=Πώς θέλετε να εγκατασταθεί το {#MyAppName};
english.ModeIntegrated=Integrate with Opsis - Desktop Customization (as a plugin)
greek.ModeIntegrated=Ενσωμάτωση στο Opsis - Desktop Customization (ως plugin)
english.ModeSeparate=Separate installation (stand-alone program)
greek.ModeSeparate=Ξεχωριστή εγκατάσταση (αυτόνομο πρόγραμμα)
english.OpsisFound=Opsis - Desktop Customization was found on this computer (version %1).
greek.OpsisFound=Βρέθηκε το Opsis - Desktop Customization σε αυτόν τον υπολογιστή (έκδοση %1).
english.OpsisNotFound=Opsis - Desktop Customization was not found on this computer. You can install the crawler separately, or integrate it now and Opsis will pick it up as soon as it is installed.
greek.OpsisNotFound=Δεν βρέθηκε το Opsis - Desktop Customization σε αυτόν τον υπολογιστή. Μπορείτε να εγκαταστήσετε το crawler ξεχωριστά ή να το ενσωματώσετε τώρα, και το Opsis θα το εντοπίσει μόλις εγκατασταθεί.
english.ModeIntegratedHelp=Integrated: the crawler appears as the "News Crawler" page of Opsis, uses its colours and language, and has no separate tray icon or shortcuts.
greek.ModeIntegratedHelp=Ενσωματωμένο: το crawler εμφανίζεται ως η σελίδα «News Crawler» του Opsis, παίρνει τα χρώματα και τη γλώσσα του και δεν έχει δικό του εικονίδιο ούτε συντομεύσεις.
english.ModeSeparateHelp=Separate: its own program with a tray icon, its own settings window and an optional start with Windows.
greek.ModeSeparateHelp=Ξεχωριστό: δικό του πρόγραμμα με εικονίδιο στην περιοχή ειδοποιήσεων, δικό του παράθυρο ρυθμίσεων και προαιρετική εκκίνηση με τα Windows.
english.BarTitle=Default bar
greek.BarTitle=Προεπιλεγμένη μπάρα
english.BarSubtitle=Which bar should be shown by default?
greek.BarSubtitle=Ποια μπάρα να εμφανίζεται από προεπιλογή;
english.BarAbove=The bar above the taskbar (touching the taskbar)
greek.BarAbove=Η μπάρα πάνω από τη γραμμή εργασιών (εφαπτόμενη σε αυτή)
english.BarInside=The bar built into the taskbar (in the empty space after the apps)
greek.BarInside=Η μπάρα ενσωματωμένη στη γραμμή εργασιών (στον κενό χώρο μετά τις εφαρμογές)
english.BarHelp=You can change this later in the settings, and you can show both bars at the same time (Bars tab).
greek.BarHelp=Μπορείτε να το αλλάξετε αργότερα στις ρυθμίσεις και να εμφανίζονται και οι δύο μπάρες μαζί (καρτέλα Μπάρες).
english.RestartOpsis=Restart Opsis - Desktop Customization: the "News Crawler" page appears in its sidebar.
greek.RestartOpsis=Επανεκκινήστε το Opsis - Desktop Customization: η σελίδα «News Crawler» θα εμφανιστεί στο πλευρικό του μενού.

[Tasks]
Name: "startup"; Description: "{cm:StartWithWindows}"; Flags: unchecked; Check: IsSeparate
Name: "desktopicon"; Description: "{cm:DesktopShortcut}"; Flags: unchecked; Check: IsSeparate

[Files]
; Separate install: the program (the plugin copy stays out, Opsis must not load a second crawler next to the stand-alone one)
Source: "bin\*"; DestDir: "{app}"; Excludes: "plugins";Flags: recursesubdirs createallsubdirs ignoreversion; Check: IsSeparate
; Integrated install: only the plugin folder, into the folder Opsis scans ({app} is set to it on the wizard page)
Source: "bin\plugins\{#PluginFolder}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion; Check: IsIntegrated

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Check: IsSeparate
Name: "{group}\{cm:UninstallShortcut}"; Filename: "{uninstallexe}"; Check: IsSeparate
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon; Check: IsSeparate

[Registry]
; Start with Windows (removed again by the uninstaller)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExe}"""; Tasks: startup; Flags: uninsdeletevalue
; Clean up the start-up entry of the very first versions (they were called DeskTicker)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "DeskTicker"; Flags: deletevalue
; Clean up the plugin-path entry written by an earlier build of this installer
Root: HKCU; Subkey: "Software\Opsis\PluginPaths"; ValueName: "OpsisNewsCrawler"; Flags: deletevalue

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent; Check: IsSeparate

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

function PluginDir(): String;
var
  Root: String;
begin
  Root := ExpandConstant('{param:PLUGINROOT|}');
  if Root = '' then Root := ExpandConstant('{userappdata}\Opsis\plugins');
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

procedure CurStepChanged(CurStep: TSetupStep);
var
  Dir: String;
begin
  if CurStep = ssPostInstall then
  begin
    // read once by the app at its next start (AppConfig.ApplyFirstRun)
    Dir := ExpandConstant('{userappdata}\OpsisNewsCrawler');
    ForceDirectories(Dir);
    SaveStringToFile(Dir + '\first-run.json', '{"Position":"' + ChosenBar() + '"}', False);
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
var
  Info: TNewStaticText;
begin
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
