# =====================================================================
# ScreenHerder uninstaller: remove the sign-in task and the DPI flag.
# =====================================================================
param([string]$ExePath)
$ErrorActionPreference = 'SilentlyContinue'
Get-ScheduledTask -TaskName 'ScreenHerder' | Unregister-ScheduledTask -Confirm:$false
if ($ExePath) {
    Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers' -Name $ExePath
}
