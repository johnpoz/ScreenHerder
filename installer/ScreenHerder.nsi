; =====================================================================
; ScreenHerder installer (NSIS 3, Modern UI 2)
;   - installs to %LOCALAPPDATA%\Programs\ScreenHerder
;   - Start menu shortcut, optional desktop shortcut
;   - optional (default on) start at sign-in with highest privileges
;   - removes an existing PersistentWindows install first
;   - normal uninstaller under Settings > Apps
; Build: makensis -DVERSION=1.0.0 -DBINDIR=<release folder> ScreenHerder.nsi
; =====================================================================
Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"

!ifndef VERSION
  !define VERSION "1.0.0"
!endif
!ifndef BINDIR
  !define BINDIR "..\Ninjacrab.PersistentWindows.Solution\SystrayShell\bin\Release"
!endif

!define APPNAME   "ScreenHerder"
!define PUBLISHER "John Pozadzides"
!define UNINSTKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\ScreenHerder"

Name "${APPNAME}"
OutFile "ScreenHerder-Setup-${VERSION}.exe"
InstallDir "$LOCALAPPDATA\Programs\ScreenHerder"
RequestExecutionLevel admin            ; required for the highest-privilege sign-in task
SetCompressor /SOLID lzma
BrandingText "${APPNAME} ${VERSION}"

VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "${APPNAME}"
VIAddVersionKey "FileDescription" "${APPNAME} Setup"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "CompanyName" "${PUBLISHER}"
VIAddVersionKey "LegalCopyright" "GPL-3.0"

; ---------------- Section 1: pages ----------------
!define MUI_ICON "..\Ninjacrab.PersistentWindows.Solution\SystrayShell\Resources\ScreenHerder.ico"
!define MUI_UNICON "..\Ninjacrab.PersistentWindows.Solution\SystrayShell\Resources\ScreenHerder.ico"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TEXT "ScreenHerder puts your windows back where they belong when you change monitors, and lets you save named layouts you can switch between from the taskbar.$\r$\n$\r$\nIf the original PersistentWindows is installed, setup removes it first so the two don't fight over your windows.$\r$\n$\r$\nClick Next to continue."
!define MUI_COMPONENTSPAGE_SMALLDESC
!define MUI_FINISHPAGE_RUN
!define MUI_FINISHPAGE_RUN_TEXT "Start ScreenHerder now"
!define MUI_FINISHPAGE_RUN_FUNCTION LaunchApp

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

; ---------------- Section 2: install ----------------
Function .onInit
  SetShellVarContext current
FunctionEnd

Section "ScreenHerder (required)" SecCore
  SectionIn RO
  SetShellVarContext current

  ; stop a running copy and remove the original PersistentWindows
  nsExec::Exec 'taskkill /F /IM ScreenHerder.exe'
  Sleep 500
  SetOutPath "$INSTDIR\setup"
  File "remove-upstream.ps1"
  File "install-task.ps1"
  File "remove-task.ps1"
  DetailPrint "Removing PersistentWindows if present..."
  nsExec::ExecToLog 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$INSTDIR\setup\remove-upstream.ps1"'

  ; program files
  SetOutPath "$INSTDIR"
  File "${BINDIR}\ScreenHerder.exe"
  File "${BINDIR}\ScreenHerder.exe.config"
  File "${BINDIR}\ScreenHerder.Engine.dll"
  File "${BINDIR}\LiteDB.dll"
  File "${BINDIR}\System.Resources.Extensions.dll"
  File "${BINDIR}\System.Memory.dll"
  File "${BINDIR}\System.Buffers.dll"
  File "${BINDIR}\System.Numerics.Vectors.dll"
  File "${BINDIR}\System.Runtime.CompilerServices.Unsafe.dll"
  File "${BINDIR}\translations.json"
  File "..\LICENSE"

  ; Start menu
  CreateShortCut "$SMPROGRAMS\ScreenHerder.lnk" "$INSTDIR\ScreenHerder.exe" "" "$INSTDIR\ScreenHerder.exe" 0

  ; uninstaller + Settings > Apps entry
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayName" "${APPNAME}"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${UNINSTKEY}" "Publisher" "${PUBLISHER}"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayIcon" "$INSTDIR\ScreenHerder.exe"
  WriteRegStr HKCU "${UNINSTKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTKEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "${UNINSTKEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoRepair" 1
SectionEnd

Section "Start ScreenHerder when I sign in" SecAutostart
  DetailPrint "Creating the sign-in task..."
  nsExec::ExecToLog 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$INSTDIR\setup\install-task.ps1" -ExePath "$INSTDIR\ScreenHerder.exe"'
SectionEnd

Section /o "Desktop shortcut" SecDesktop
  SetShellVarContext current
  CreateShortCut "$DESKTOP\ScreenHerder.lnk" "$INSTDIR\ScreenHerder.exe" "" "$INSTDIR\ScreenHerder.exe" 0
SectionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecCore} "The ScreenHerder app and its Start menu entry."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecAutostart} "Recommended. Starts ScreenHerder automatically, with the rights it needs to move every window."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop} "Adds a ScreenHerder icon to the desktop."
!insertmacro MUI_FUNCTION_DESCRIPTION_END

; start through the sign-in task when it exists, so the app runs the same
; way it will after every sign-in
Function LaunchApp
  ${If} ${SectionIsSelected} ${SecAutostart}
    nsExec::Exec 'schtasks.exe /Run /TN "ScreenHerder"'
  ${Else}
    Exec '"$INSTDIR\ScreenHerder.exe"'
  ${EndIf}
FunctionEnd

; ---------------- Section 3: uninstall ----------------
Function un.onInit
  SetShellVarContext current
FunctionEnd

Section "Uninstall"
  SetShellVarContext current
  nsExec::Exec 'taskkill /F /IM ScreenHerder.exe'
  Sleep 500
  nsExec::ExecToLog 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$INSTDIR\setup\remove-task.ps1" -ExePath "$INSTDIR\ScreenHerder.exe"'

  Delete "$SMPROGRAMS\ScreenHerder.lnk"
  Delete "$DESKTOP\ScreenHerder.lnk"
  RMDir /r "$INSTDIR"
  RMDir /r "$LOCALAPPDATA\ScreenHerder"     ; settings, layouts and window history
  DeleteRegKey HKCU "${UNINSTKEY}"
SectionEnd
