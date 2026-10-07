using System;
using System.Windows.Forms;
using Microsoft.Win32;

using PersistentWindows.Common.Diagnostics;

namespace PersistentWindows.SystrayShell
{
    // =====================================================================
    // "Start ScreenHerder when I sign in".
    // Uses the normal per-user startup entry
    // (HKCU\Software\Microsoft\Windows\CurrentVersion\Run), the same place
    // Windows' own Startup apps list reads. No administrator rights and no
    // permission prompt; the user can also switch it off in Task Manager >
    // Startup apps or Settings > Apps > Startup.
    // =====================================================================
    public static class AutoStart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "ScreenHerder";

        private static string Command
        {
            get { return "\"" + Application.ExecutablePath + "\""; }
        }

        // ---------------- Section 1: availability and current state ----------------
        // a copy run straight from a build folder shouldn't register itself
        public static bool CanManage
        {
            get
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Application.ExecutablePath.StartsWith(local, StringComparison.OrdinalIgnoreCase)
                    || Application.ExecutablePath.IndexOf("\\Program Files", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        public static bool IsEnabled()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue(ValueName) != null;
            }
            catch (Exception ex)
            {
                Log.Error("autostart read failed: " + ex.Message);
                return false;
            }
        }

        // ---------------- Section 2: turn it on or off ----------------
        public static bool Set(bool on)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (on)
                        k.SetValue(ValueName, Command);
                    else
                        k.DeleteValue(ValueName, false);
                }
                return IsEnabled() == on;
            }
            catch (Exception ex)
            {
                Log.Error("autostart change failed: " + ex.Message);
                return false;
            }
        }
    }
}
