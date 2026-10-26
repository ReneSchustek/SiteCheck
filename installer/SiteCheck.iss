; Inno-Setup-Skript für SiteCheck — erzeugt eine Setup.exe aus dem self-contained Publish
; (dist\setup-publish). Per-User-Installation ohne Adminrechte.
;
; Build (vom Repo-Root):
;   tools\publish-setup.ps1            -> publiziert + kompiliert die Setup.exe
; oder direkt:
;   ISCC.exe /DMyAppVersion=0.1.0 installer\SiteCheck.iss

#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif
#define MyAppName "SiteCheck"
#define MyAppPublisher "Ruhrcoder (Rene Schustek)"
#define MyAppURL "https://ruhrcoder.de"
#define MyAppExeName "SiteCheck.exe"
#ifndef PublishDir
  #define PublishDir "..\dist\setup-publish"
#endif

[Setup]
; Stabile AppId (nicht ändern — identifiziert die Installation für Upgrade/Deinstallation).
AppId={{8C3A1E7D-5B29-4F6C-A1D8-3E9B2C4F7A60}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableDirPage=no
AllowUNCPath=no
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=SiteCheck-Setup-v{#MyAppVersion}
SetupIconFile=..\src\SiteCheck.App\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} {#MyAppVersion}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoProductName={#MyAppName}

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
