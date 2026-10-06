# =====================================================================
# ScreenHerder: start at sign-in.
# Creates a Task Scheduler task that launches ScreenHerder with highest
# privileges when the current user signs in (needed to restore windows
# of elevated apps), plus the high-DPI compatibility flag that keeps
# restores accurate on fractionally scaled displays (125%, 150%).
# =====================================================================
param([Parameter(Mandatory = $true)][string]$ExePath)
$ErrorActionPreference = 'Stop'

# ---- Section 1: high-DPI awareness flag for the exe ----
$layers = 'HKCU:\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers'
if (-not (Test-Path $layers)) { New-Item -Path $layers -Force | Out-Null }
Set-ItemProperty -Path $layers -Name $ExePath -Value '~ HIGHDPIAWARE'

# ---- Section 2: replace any earlier ScreenHerder task ----
$taskName = 'ScreenHerder'
Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue |
    Unregister-ScheduledTask -Confirm:$false

# ---- Section 3: create the task ----
$user = "$env:USERDOMAIN\$env:USERNAME"
$action = New-ScheduledTaskAction -Execute $ExePath -WorkingDirectory (Split-Path $ExePath)
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $user
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew -Priority 6
$principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Highest
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings `
    -Principal $principal -Description 'Starts ScreenHerder when you sign in.' | Out-Null
