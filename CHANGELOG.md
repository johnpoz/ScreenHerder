# ScreenHerder Changelog

## 1.3.0 (2026-10-06)

- Loading a layout reopens apps from it that aren't running and moves them into place. Store apps (Calculator, Photos and the like) are included on a best-effort basis, and Explorer folder windows reopen to the same folder. Programs start as the normal user, never with ScreenHerder's administrator rights.
- Apps that have been uninstalled are skipped and their spot is left empty; a notice lists anything that couldn't be reopened.
- An app with several windows is launched once; the windows it opens go to the saved spots in order.
- Only when loading a named layout, never after a monitor change. On by default; Settings > Restore > "Reopen closed apps when I load a layout" turns it off.
- Layouts now record each window's program, start-up arguments and position when saved. Layouts saved before 1.3.0 need to be saved again to reopen apps.
- The old "Reopen apps that are part of a layout but were closed" option was wired to an engine feature ScreenHerder never used, so it did nothing; it's replaced by the setting above.

## 1.2.2 (2026-10-06)

- Fix: windows and desktop icons could be impossible to move on a laptop. ScreenHerder (and the PersistentWindows engine underneath) restored on every Windows display event, including ones where the monitors hadn't changed at all (refresh-rate switching, docks waking, lock and unlock, sleep and wake), snapping things back while you moved them. Now nothing is restored unless the set of connected monitors actually changed; events with the same monitors are logged and ignored.
- Desktop icon tracking never talks to Explorer while a mouse button is held, so it can't interfere with dragging icons.

- Hover tips wrap at about 70 characters. In 1.2.x long tips ran as one line off the edge of the screen (found while taking screenshots on MS-S1).
- README: screenshots of the tray icon, quick menu, Save Desktop As, right-click menu and Settings (docs/screenshots).
- Verified on MS-S1 with 1.2.0: Save Desktop As via shortcut, letter-key restore from the quick menu, Undo enabled after a restore, all Settings tabs render.

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
