; Inno Setup script for PlexusX - builds a classic install wizard
; (welcome -> choose folder -> shortcuts -> install -> finish).
;
; Compile with: ISCC.exe VibranceHud.iss
; ...after publishing a self-contained build (no .NET runtime needed on the target PC):
;   dotnet publish -c Release -o publish
; The self-contained/single-file settings live in VibranceHud.csproj, so this plain
; command is enough - no easy-to-forget -r/--self-contained flags to remember by hand.

#define AppVersion "0.2.1"

[Setup]
AppId={{8F3A1C2B-4D5E-4F6A-9B7C-1234567890AB}}
AppName=PlexusX
AppVersion={#AppVersion}
AppPublisher=PlexusX
DefaultDirName={localappdata}\Programs\PlexusX
; Always install into the app's own folder - do NOT let the user pick a folder
; like the Desktop, which would scatter hundreds of .NET files loose.
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=installer
OutputBaseFilename=PlexusX-Setup-{#AppVersion}
SetupIconFile=brand\PlexusX.ico
; Let the installer close a running PlexusX so in-place updates don't fail on locked files.
; This alone isn't enough for a tray app (it can ignore the polite close request), so the
; [Code] section below also force-kills PlexusX.exe right before files are replaced - that's
; what fixes "DeleteFile failed; code 5 (Access denied)" when installing over a running copy.
CloseApplications=yes
RestartApplications=no
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName=PlexusX
UninstallDisplayIcon={app}\PlexusX.exe

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

; Wipe the install folder before laying down the new files. Older builds were
; framework-dependent and scattered ~200 loose .NET DLLs here (Accessibility.dll,
; coreclr.dll, the old Velopack.dll, etc.). The new build is a single self-contained
; PlexusX.exe and doesn't need any of them - leaving them behind is both the "garbage
; files" mess and the source of the locked-file install errors. User settings live in
; %APPDATA%\PlexusX (a different folder), so this does NOT touch anything the user cares
; about keeping.
[InstallDelete]
Type: filesandordirs; Name: "{app}\*"

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{autoprograms}\PlexusX"; Filename: "{app}\PlexusX.exe"
Name: "{autodesktop}\PlexusX"; Filename: "{app}\PlexusX.exe"; Tasks: desktopicon

[Run]
; No "skipifsilent": a silent auto-update must relaunch PlexusX afterwards.
Filename: "{app}\PlexusX.exe"; Description: "Launch PlexusX now"; Flags: nowait postinstall

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
  begin
    // PlexusX runs from the system tray, so its files stay locked while it's open -
    // and a tray app can quietly ignore Inno's polite Restart-Manager close request.
    // Force-terminate it (both a normal launch and an auto-update relaunch) before we
    // touch any files. The setup process itself is named differently, so this can't
    // kill the installer. Runs in silent mode too, so auto-updates get the same fix.
    Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM PlexusX.exe', '',
      SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(700); // give Windows a moment to release the file handles
  end;
end;
