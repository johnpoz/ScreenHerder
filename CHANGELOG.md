# ScreenHerder Changelog

## 1.2.1 (2026-10-06)

- Credits now say ScreenHerder includes the PersistentWindows window-tracking engine and list what ScreenHerder adds (Help window, splash, file properties, README).
- README: Download section linking the latest release, with what to do when Windows warns the installer is unsigned.

## 1.2.0 (2026-10-06)

- Settings > General: "Start ScreenHerder when I sign in" turns the sign-in task on or off (asks Windows for permission because it edits a scheduled task).
- Settings > General: "Minimize ScreenHerder's windows to the tray". Minimized Settings and Help windows leave the taskbar; right-click the sheep to bring them back.
- Hover tips: every option explains itself after the pointer rests on it for 3 seconds; tray menu items have tips too. "Show tips when I hover over an option" turns them off.
- First Windows run (1.1.0 on MS-S1-HOME, 2026-10-06): started from the sign-in task, display key detected, 19 desktop icons captured.

## 1.1.0 (2026-10-05)

- Desktop icons are restored with windows: after monitor changes (recording pauses during the change; two restore passes after it settles) and with named layouts and Undo. Deleted icons leave a gap; new icons stay put. Auto arrange is switched off when restoring.
- Settings > Restore: "Put desktop icons back too" (on by default).

## 1.0.1 (2026-10-05)

- Sheep icon (teal idle, orange while restoring).

## 1.0.0 (2026-10-05)

- Left-click quick menu: recent named layouts for the connected monitors, Save Desktop As, Undo Last Restore, All Layouts; letter keys restore, Shift + letter saves.
- Right-click menu: Settings, Help, Exit. Double-click removed.
- Settings window (General, Restore, Shortcuts, Layouts, Advanced), system-wide shortcuts, named layouts stored in layouts.json.
- Update checker removed. Renamed to ScreenHerder (exe, engine DLL, data folder, branding).
- NSIS installer with sign-in task, PersistentWindows removal, and uninstaller.
- Built from PersistentWindows 5.77 (97356d4), retargeted to .NET Framework 4.8 so it compiles with the .NET SDK on Linux.
