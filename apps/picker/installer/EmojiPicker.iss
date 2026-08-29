; Modern Emoji Picker MVP — self-contained per-user installer only.
; Build only through scripts\release.ps1 so qualification, identity, notices,
; checksums and package policy are verified before this compiler is invoked.

#ifndef AppVersion
  #error AppVersion must be supplied by scripts\release.ps1
#endif

#ifndef PublishDir
  #error PublishDir must be supplied by scripts\release.ps1
#endif

#ifndef OutputDir
  #error OutputDir must be supplied by scripts\release.ps1
#endif

#define AppName "Modern Emoji Picker"
#define AppExe "ModernEmojiPicker.exe"
#define AppPublisher "X CroSs"
#define AppUrl "https://github.com/xcrossth/Modern-Emoji-Panel"
#define AppDataDirectory "ModernEmojiPicker"

[Setup]
AppId={{6AFB6AF4-F41A-412A-8749-9BF9FD673855}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppSupportURL={#AppUrl}
DefaultDirName={localappdata}\Programs\Modern Emoji Picker
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
OutputDir={#OutputDir}
OutputBaseFilename=Modern-Emoji-Picker-v{#AppVersion}-setup-win-x64
SetupIconFile=..\EmojiPicker\Resources\modern-emoji-picker.ico
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startup"; Description: "Start Modern Emoji Picker automatically when I sign in"; GroupDescription: "Startup:"; Flags: checkedonce

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "ModernEmojiPicker"; ValueData: """{app}\{#AppExe}"""; \
    Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch Modern Emoji Picker now"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /IM {#AppExe} /F"; Flags: runhidden; RunOnceId: "StopModernEmojiPicker"

[Code]
var
  DeleteUserDataCheckBox: TNewCheckBox;

function InitializeUninstall(): Boolean;
begin
  Result := True;
  DeleteUserDataCheckBox := TNewCheckBox.Create(UninstallProgressForm);
  DeleteUserDataCheckBox.Parent := UninstallProgressForm.InnerPage;
  DeleteUserDataCheckBox.Left := UninstallProgressForm.StatusLabel.Left;
  DeleteUserDataCheckBox.Top := UninstallProgressForm.StatusLabel.Top + ScaleY(38);
  DeleteUserDataCheckBox.Width := UninstallProgressForm.InnerPage.ClientWidth - DeleteUserDataCheckBox.Left;
  DeleteUserDataCheckBox.Caption := 'Delete Modern Emoji Picker Settings and Activity Data';
  DeleteUserDataCheckBox.Checked := False;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and DeleteUserDataCheckBox.Checked then
    DelTree(ExpandConstant('{userappdata}\{#AppDataDirectory}'), True, True, True);
end;
