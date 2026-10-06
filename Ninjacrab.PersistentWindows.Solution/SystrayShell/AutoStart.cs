using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

using PersistentWindows.Common.Diagnostics;

namespace PersistentWindows.SystrayShell
{
    // =====================================================================
    // "Start ScreenHerder when I sign in".
    // The switch is the Task Scheduler task named "ScreenHerder" that the
    // installer creates; there is no separate setting to drift out of sync.
    // Creating or deleting that task needs administrator rights, so the
    // change runs in an elevated PowerShell (Windows asks for permission
    // unless ScreenHerder is already running elevated).
    // =====================================================================
    public static class AutoStart
    {
        private const string TaskName = "ScreenHerder";

        private static string SetupDir
        {
            get { return Path.Combine(Application.StartupPath, "setup"); }
        }

        private static string InstallScript
        {
            get { return Path.Combine(SetupDir, "install-task.ps1"); }
        }

        // ---------------- Section 1: availability and current state ----------------
        // only an installed copy (with the setup scripts) can manage the task
        public static bool CanManage
        {
            get { return File.Exists(InstallScript); }
        }

        public static bool IsEnabled()
        {
            try
            {
                var psi = new ProcessStartInfo("schtasks.exe", "/Query /TN \"" + TaskName + "\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd();
                    p.WaitForExit(15000);
                    return p.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Log.Error("autostart query failed: " + ex.Message);
                return false;
            }
        }

        // ---------------- Section 2: turn it on or off ----------------
        // returns true when the task ends up in the requested state
        public static bool Set(bool on)
        {
            string exe = Application.ExecutablePath;
            string args = on
                ? "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + InstallScript + "\" -ExePath \"" + exe + "\""
                : "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command \"Unregister-ScheduledTask -TaskName '" + TaskName + "' -Confirm:$false\"";
            try
            {
                var psi = new ProcessStartInfo("powershell.exe", args)
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (var p = Process.Start(psi))
                    p.WaitForExit(60000);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // the user said No to the Windows permission prompt
                return false;
            }
            catch (Exception ex)
            {
                Log.Error("autostart change failed: " + ex.Message);
                return false;
            }
            return IsEnabled() == on;
        }
    }
}
