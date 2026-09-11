#ifndef SourceDir
  #error SourceDir is required. Build using build-installer.ps1 to select a complete publish stage.
#endif

#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

#ifndef AppVersion
  #define AppVersion "8.0.0"
#endif

#define AppName "Naufal Tech's Windows Powertoys"
#define AppExeName "Naufal Windows Powertoys.exe"
#define AppPublisher "Naufal Tech's Ltd."
#define AppUserModelId "NaufalTechs.WindowsPowertoys"
#define AppIconFile "{app}\Assets\NaufalWindowsPowertoys.ico"

[Setup]
AppId={{A75F9775-AC15-4F03-8931-43D04EA6B032}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\Naufal Tech's Limited\Naufal Windows Powertoys
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
SetupArchitecture=x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir={#OutputDir}
OutputBaseFilename=Naufal-Windows-Powertoys-Setup-{#AppVersion}-x64
SetupIconFile=..\Assets\NaufalWindowsPowertoys.ico
UninstallDisplayIcon={#AppIconFile}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
DisableWelcomePage=no
CloseApplications=yes
RestartApplications=no
; Do not let an older flat Program Files path replace the new company folder.
UsePreviousAppDir=no
UsePreviousGroup=yes
VersionInfoVersion={#AppVersion}.0
VersionInfoProductVersion={#AppVersion}.0
VersionInfoProductName={#AppName}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Installer

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{#AppIconFile}"; IconIndex: 0; AppUserModelID: "{#AppUserModelId}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{#AppIconFile}"; IconIndex: 0; AppUserModelID: "{#AppUserModelId}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  PreviousInstallDir: String;
begin
  Result := '';
  { Moving an existing AppId to a different directory would leave its old
    files/uninstall log behind while replacing its uninstall registration.
    Require a normal uninstall first; never delete or run an uninstaller here. }
  if RegQueryStringValue(HKLM64,
    'Software\Microsoft\Windows\CurrentVersion\Uninstall\{A75F9775-AC15-4F03-8931-43D04EA6B032}_is1',
    'Inno Setup: App Path', PreviousInstallDir) then
  begin
    if (PreviousInstallDir <> '') and
      (CompareText(RemoveBackslash(ExpandFileName(PreviousInstallDir)),
        RemoveBackslash(ExpandFileName(ExpandConstant('{app}')))) <> 0) then
      Result := 'An existing installation is registered at:' + #13#10 +
        PreviousInstallDir + #13#10#13#10 +
        'Wait for all running tasks to finish, close the application, then uninstall ' +
        'the existing version from Windows Settings > Apps before installing in the new folder.' + #13#10#13#10 +
        'Setup has not moved or deleted the old installation.';
  end;
end;
