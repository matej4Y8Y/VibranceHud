; Inno Setup script for Vibrance HUD - builds a classic install wizard
; (welcome -> choose folder -> shortcuts -> install -> finish).
; Compile with: ISCC.exe VibranceHud.iss   (after `dotnet publish ... -o publish`)

#define AppVersion "0.2.0"

[Setup]
AppId={{8F3A1C2B-4D5E-4F6A-9B7C-1234567890AB}}
AppName=Vibrance HUD
AppVersion={#AppVersion}
AppPublisher=Vibrance HUD
DefaultDirName={localappdata}\Programs\Vibrance HUD
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=installer
OutputBaseFilename=VibranceHUD-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName=Vibrance HUD
UninstallDisplayIcon={app}\VibranceHud.exe

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{autoprograms}\Vibrance HUD"; Filename: "{app}\VibranceHud.exe"
Name: "{autodesktop}\Vibrance HUD"; Filename: "{app}\VibranceHud.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\VibranceHud.exe"; Description: "Launch Vibrance HUD now"; Flags: nowait postinstall skipifsilent
