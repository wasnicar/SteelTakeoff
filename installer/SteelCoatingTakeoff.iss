; ============================================================================
;  Steel Coating Takeoff - Inno Setup script
;  Produces a real Windows installer: UAC-elevated, installs to Program Files,
;  registers an uninstaller in Add/Remove Programs, Start Menu + Desktop icons.
;  Build with:  installer\build-installer.ps1
; ============================================================================

#define AppName        "Steel Coating Takeoff"
#define AppPublisher   "Asnicar & Associates"
#define AppExeName     "SteelCoatingTakeoff.exe"
#ifndef AppVersion
  #define AppVersion   "2.5.0"
#endif
#ifndef StageDir
  #define StageDir     "..\dist\stage\SteelCoatingTakeoff"
#endif
#ifndef OutDir
  #define OutDir       "..\dist"
#endif

; Two builds from this one script:
;   (default)   admin install into Program Files  -> SteelCoatingTakeoffSetup.exe
;   /DPerUser   no-admin install into LocalAppData -> SteelCoatingTakeoffSetup-NoAdmin.exe
; They carry different AppIds so both can exist without fighting over one
; Add/Remove Programs entry.
[Setup]
#ifdef PerUser
AppId={{C7A4E913-25D8-4F60-B1A7-6E9D48C3502F}
DefaultDirName={localappdata}\Programs\{#AppName}
OutputBaseFilename=SteelCoatingTakeoffSetup-NoAdmin
; No elevation: installs for the current user only, no UAC prompt, no admin rights.
PrivilegesRequired=lowest
UninstallDisplayName={#AppName} (per-user)
#else
AppId={{8E1C2F4A-7B93-4D62-9E4B-2C5A7D3F1B08}
DefaultDirName={autopf}\{#AppName}
OutputBaseFilename=SteelCoatingTakeoffSetup
; Program Files needs admin; this also puts the UAC shield on the setup exe.
PrivilegesRequired=admin
UninstallDisplayName={#AppName}
#endif
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir={#OutDir}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; The app and the bundled Sage SDK are x64.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
DisableDirPage=no
AllowNoIcons=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
; App binaries + config
Source: "{#StageDir}\SteelCoatingTakeoff.exe";        DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\SteelCoatingTakeoff.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\SteelCoatingTakeoff.Core.dll";   DestDir: "{app}"; Flags: ignoreversion
; appsettings.json is user-editable config: install it but never clobber an existing one.
Source: "{#StageDir}\appsettings.json";               DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall
; Bundled Sage Estimating SDK (resolved at runtime from .\Sdk)
Source: "{#StageDir}\Sdk\*";                          DestDir: "{app}\Sdk"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}";        Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";  Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Settings the app writes next to itself
Type: files; Name: "{app}\appsettings.json"
Type: dirifempty; Name: "{app}"
