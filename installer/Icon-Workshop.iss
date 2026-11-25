; Installer for Icon Workshop (Inno Setup 6)
; Build: powershell -ExecutionPolicy Bypass -File installer\build.ps1
; Expects the published app in ..\dist\publish (see build.ps1).

#define AppName      "Icon Workshop"
#define AppExe       "IconWorkshop.exe"
#define AppPublisher "Ronny Severin"
#define AppUrl       "https://github.com/RNYSVRN/Icon-Workshop"
#define PublishDir   "..\dist\publish"
#define VerMajor
#define VerMinor
#define VerRev
#define VerBuild
#expr GetVersionComponents(PublishDir + "\" + AppExe, VerMajor, VerMinor, VerRev, VerBuild)
#define AppVersion   Str(VerMajor) + "." + Str(VerMinor) + "." + Str(VerRev)

[Setup]
; Fixed AppId: stays the same across versions so updates replace the previous installation
AppId={{7B35C896-B73D-47BB-9945-4A207EACED36}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\dist
OutputBaseFilename=Icon-Workshop-Setup-{#AppVersion}
SetupIconFile=..\src\IconWorkshop\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Setup
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
WizardStyle=modern
; Icon Workshop changes icons per user and needs no admin rights: install for the current user by default
; (%LocalAppData%\Programs), optionally for all users ("Program Files").
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
; Detect a running instance before update/uninstall and let it be closed
AppMutex=IconWorkshopRunning
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "de"; MessagesFile: "compiler:Languages\German.isl"

[CustomMessages]
en.LaunchApp=Start {#AppName}
de.LaunchApp={#AppName} starten

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Remove what the app registered in Windows: Explorer context menu entry and sign-in check (both per user)
Filename: "{app}\{#AppExe}"; Parameters: "--uninstall"; Flags: runhidden waituntilterminated; RunOnceId: "RemoveRegistrations"

; The data folder %LocalAppData%\Icon-Workshop (icon packs, backups, assigned ICO files) is kept on uninstall,
; so assigned icons keep working. Restore originals in the app first, or delete the folder to remove everything.
