; Inno Setup script for PlexusX - builds a classic install wizard
; (welcome -> choose folder -> shortcuts -> install -> finish).
; Compile with: ISCC.exe VibranceHud.iss   (after `dotnet publish ... -o publish`)

#define AppVersion "0.2.0"

[Setup]
AppId={{8F3A1C2B-4D5E-4F6A-9B7C-1234567890AB}}
AppName=PlexusX
AppVersion={#AppVersion}
AppPublisher=PlexusX
DefaultDirName={localappdata}\Programs\PlexusX
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=installer
OutputBaseFilename=PlexusX-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName=PlexusX
UninstallDisplayIcon={app}\PlexusX.exe

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{autoprograms}\PlexusX"; Filename: "{app}\PlexusX.exe"
Name: "{autodesktop}\PlexusX"; Filename: "{app}\PlexusX.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\PlexusX.exe"; Description: "Launch PlexusX now"; Flags: nowait postinstall skipifsilent
