# qa-click.ps1 — click at window-relative coordinates inside the PlexusX window.
# Used by the plexusx-qa skill to drive page navigation during visual sweeps.
# Usage: powershell -File qa-click.ps1 -X 90 -Y 300
param(
    [Parameter(Mandatory=$true)][int]$X,
    [Parameter(Mandatory=$true)][int]$Y,
    [string]$WindowTitlePart = 'PlexusX'
)
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class QaClick {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
    [DllImport("user32.dll")] public static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
$procs = Get-Process PlexusX -ErrorAction SilentlyContinue
if (-not $procs) { Write-Output 'FAIL: PlexusX not running'; exit 1 }
$hwnd = $procs[0].MainWindowHandle
if ($hwnd -eq 0) { Write-Output 'FAIL: no main window (tray-only?)'; exit 1 }
$r = New-Object QaClick+RECT
[QaClick]::GetWindowRect($hwnd, [ref]$r) | Out-Null
[QaClick]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 300
$cx = $r.Left + $X
$cy = $r.Top + $Y
[QaClick]::SetCursorPos($cx, $cy) | Out-Null
Start-Sleep -Milliseconds 100
[QaClick]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)  # LEFTDOWN
Start-Sleep -Milliseconds 50
[QaClick]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)  # LEFTUP
Write-Output "clicked $X,$Y (window at $($r.Left),$($r.Top))"
exit 0
