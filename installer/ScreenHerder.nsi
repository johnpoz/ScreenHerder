; =====================================================================
; ScreenHerder installer (NSIS 3, Modern UI 2)
;   - per-user install to %LOCALAPPDATA%\Programs\ScreenHerder
;   - no administrator rights, no PowerShell, no permission prompt
;   - Start menu shortcut, optional desktop shortcut
;   - optional (default on) start at sign-in via the per-user Run entry
;   - stops and removes a per-user PersistentWindows if present
;   - normal uninstaller under Settings > Apps
; Build: makensis -DVERSION=1.3.1 -DBINDIR=<release folder> ScreenHerder.nsi
; =====================================================================
Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"

!ifndef VERSION
  !define VERSION "1.3.1"
!endif
!ifndef BINDIR
  !define BINDIR "..\Ninjacrab.PersistentWindows.Solution\SystrayShell\bin\Release"
!endif

!define APPNAME   "ScreenHerder"
!define PUBLISHER "John Pozadzides"
!define UNINSTKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\ScreenHerder"
!define RUNKEY    "Software\Microsoft\Windows\CurrentVersion\Run"

Name "${APPNAME}"
OutFile "ScreenHerder-Setup-${VERSION}.exe"
InstallDir "$LOCALAPPDATA\Programs\ScreenHerder"
RequestExecutionLevel user
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
!define MUI_WELCOMEPAGE_TEXT "ScreenHerder puts your windows and desktop icons back where they belong when you change monitors, and lets you save named layouts you can switch between from the taskbar.$\r$\n$\r$\nIt installs just for you and doesn't need administrator rights.$\r$\n$\r$\nClick Next to continue."
!define MUI_COMPONENTSPAGE_SMALLDESC
!define MUI_FINISHPAGE_RUN "$INSTDIR\ScreenHerder.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Start ScreenHerder now"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

; ---------------- Section 2: install ----------------
Section "ScreenHerder (required)" SecCore
  SectionIn RO

  ; close a running copy, and the original PersistentWindows if present
  nsExec::Exec 'taskkill /IM ScreenHerder.exe'
  nsExec::Exec 'taskkill /IM PersistentWindows.exe'
  Sleep 1000
  nsExec::Exec 'taskkill /F /IM ScreenHerder.exe'

  ; older ScreenHerder versions kept setup scripts here
  RMDir /r "$INSTDIR\setup"

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

  CreateShortCut "$SMPROGRAMS\ScreenHerder.lnk" "$INSTDIR\ScreenHerder.exe" "" "$INSTDIR\ScreenHerder.exe" 0

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
  WriteRegStr HKCU "${RUNKEY}" "ScreenHerder" '"$INSTDIR\ScreenHerder.exe"'
SectionEnd

Section /o "Desktop shortcut" SecDesktop
  CreateShortCut "$DESKTOP\ScreenHerder.lnk" "$INSTDIR\ScreenHerder.exe" "" "$INSTDIR\ScreenHerder.exe" 0
SectionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecCore} "The ScreenHerder app and its Start menu entry."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecAutostart} "Recommended. Starts ScreenHerder automatically when you sign in to Windows."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop} "Adds a ScreenHerder icon to the desktop."
!insertmacro MUI_FUNCTION_DESCRIPTION_END

; ---------------- Section 3: uninstall ----------------
Section "Uninstall"
  nsExec::Exec 'taskkill /IM ScreenHerder.exe'
  Sleep 1000
  nsExec::Exec 'taskkill /F /IM ScreenHerder.exe'

  DeleteRegValue HKCU "${RUNKEY}" "ScreenHerder"
  Delete "$SMPROGRAMS\ScreenHerder.lnk"
  Delete "$DESKTOP\ScreenHerder.lnk"
  RMDir /r "$INSTDIR"
  RMDir /r "$LOCALAPPDATA\ScreenHerder"     ; settings, layouts and window history
  DeleteRegKey HKCU "${UNINSTKEY}"
SectionEnd
