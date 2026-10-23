; The Windows installer (docs/platforms.md): the release's win-x64 bundle,
; installed for the user who runs it, with no admin rights asked. That keeps
; the install folder the user's own, so the game can update itself in place
; (docs/updates.md), as it cannot in Program Files. Built by the release
; workflow on a Windows runner with Inno Setup:
;   iscc /DAppVersion=0.1.5 /DSourceDir=path\to\OpenPax-0.1.5-win-x64 /DOutputDir=out openpax.iss
; None of the game is in it: the first run imports the player's own copy.

#ifndef AppVersion
  #error Pass /DAppVersion=VERSION
#endif
#ifndef SourceDir
  #error Pass /DSourceDir=the unpacked win-x64 bundle
#endif
#ifndef OutputDir
  #define OutputDir "."
#endif

[Setup]
; The same for every version, so a new installer upgrades the old install.
AppId={{4BE535CC-F7F0-46F9-9CCE-759A5AD4059E}
AppName=OpenPax
AppVersion={#AppVersion}
AppVerName=OpenPax {#AppVersion}
AppPublisher=OpenPax
AppPublisherURL=https://github.com/jlac1024/OpenPax
AppSupportURL=https://github.com/jlac1024/OpenPax/issues
AppUpdatesURL=https://github.com/jlac1024/OpenPax/releases
; For this user only: %LOCALAPPDATA%\Programs\OpenPax, no admin prompt.
PrivilegesRequired=lowest
DefaultDirName={autopf}\OpenPax
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\openpax.exe
; OpenPax's own icon (src/OpenPax.Host/Icon, tools/make_icon.sh).
SetupIconFile=..\..\src\OpenPax.Host\Icon\openpax.ico
UninstallDisplayName=OpenPax
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=OpenPax-{#AppVersion}-win-x64-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; The game closes itself to update; an open one is asked to close first here too.
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "Make a desktop shortcut"; GroupDescription: "Shortcuts:"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "config.json"; Flags: ignoreversion recursesubdirs createallsubdirs
; config.json is edited by hand (the lobby's address): an upgrade keeps the player's.
Source: "{#SourceDir}\config.json"; DestDir: "{app}"; Flags: onlyifdoesntexist

[Icons]
Name: "{autoprograms}\OpenPax"; Filename: "{app}\openpax.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\OpenPax"; Filename: "{app}\openpax.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\openpax.exe"; Description: "Start OpenPax"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; What the game's own updates leave in the install folder (docs/updates.md).
Type: filesandordirs; Name: "{app}\.openpax-old"
Type: filesandordirs; Name: "{app}\.openpax-update"
