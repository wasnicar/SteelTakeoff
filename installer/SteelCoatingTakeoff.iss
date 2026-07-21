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

[Setup]
AppId={{8E1C2F4A-7B93-4D62-9E4B-2C5A7D3F1B08}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir={#OutDir}
OutputBaseFilename=SteelCoatingTakeoffSetup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Program Files needs admin; this also puts the UAC shield on the setup exe.
PrivilegesRequired=admin
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
