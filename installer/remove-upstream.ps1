# =====================================================================
# ScreenHerder installer: remove the original PersistentWindows if it
# is present, so the two never fight over window positions. Its saved
# data in %LOCALAPPDATA%\PersistentWindows is left alone.
# =====================================================================
$ErrorActionPreference = 'SilentlyContinue'

# ---- Section 1: stop it ----
Get-Process -Name PersistentWindows | Stop-Process -Force

# ---- Section 2: remove its sign-in task(s) ----
Get-ScheduledTask | Where-Object { $_.TaskName -like 'StartPersistentWindows*' } |
    Unregister-ScheduledTask -Confirm:$false

# ---- Section 3: uninstall a winget copy ----
$winget = Get-Command winget -ErrorAction SilentlyContinue
if ($winget) {
    $list = & winget list --name PersistentWindows --disable-interactivity --accept-source-agreements 2>$null | Out-String
    if ($list -match 'PersistentWindows') {
        & winget uninstall --name PersistentWindows --silent --disable-interactivity --accept-source-agreements 2>$null | Out-Null
    }
}
