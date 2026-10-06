using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows.Forms;

using PersistentWindows.Common.Diagnostics;

namespace PersistentWindows.SystrayShell
{
    // =====================================================================
    // ScreenHerder user settings.
    // Stored as settings.json in %LOCALAPPDATA%\ScreenHerder and applied
    // to the window engine at startup. Saving from the Settings window
    // restarts the app so every engine option takes effect cleanly.
    // =====================================================================
    [DataContract]
    public class ShSettings
    {
        public const string FileName = "settings.json";

        // ---------------- General ----------------
        [DataMember] public int QuickMenuCount;           // layouts listed in the left-click menu
        [DataMember] public bool ShowSplash;               // brief splash at startup
        [DataMember] public bool NotifyOnRestore;          // balloon when an automatic restore runs
        [DataMember] public bool AskBeforeAutoRestore;     // prompt before restoring after a monitor change
        [DataMember] public bool MinimizeToTray;           // minimized Settings/Help windows hide in the tray
        [DataMember] public bool ShowHoverTips;            // 3-second hover explanations on options

        // ---------------- Restore behavior ----------------
        [DataMember] public double RestoreDelaySeconds;    // wait after a monitor change before restoring (0 = automatic)
        [DataMember] public int ZOrderMode;                // 0 never, 1 saved layouts only, 2 always
        [DataMember] public bool FastRestore;
        [DataMember] public bool FixOffscreen;             // pull windows that end up off-screen back on
        [DataMember] public bool EnhancedOffscreenFix;
        [DataMember] public bool FixTaskbar;               // restore taskbar position too
        [DataMember] public bool FixUnminimized;
        [DataMember] public bool RestoreNewWindowsToLastPosition;
        [DataMember] public bool RestoreClosedWindows;     // (unused since 1.3.0; engine disk-database feature)
        [DataMember] public bool ReopenClosedApps;         // loading a layout starts apps that aren't running
        [DataMember] public bool ShowDesktopWhenDisplayChanges;
        [DataMember] public bool RestoreDesktopIcons;      // desktop icons go back with the windows

        // ---------------- Shortcuts (System.Windows.Forms.Keys incl. modifiers; 0 = none) ----------------
        [DataMember] public int HotkeyQuickMenu;
        [DataMember] public int HotkeySaveAs;
        [DataMember] public int HotkeyUndo;

        // ---------------- Advanced ----------------
        [DataMember] public string IgnoreProcesses;        // semicolon separated process names
        [DataMember] public double CaptureDelaySeconds;    // wait after a window moves before recording it (0 = automatic)
        [DataMember] public bool CtrlMinimizeToTray;
        [DataMember] public bool SwapOnAltActivate;
        [DataMember] public bool DualPosition;
        [DataMember] public bool WindowCommander;          // Alt+W browser window commander

        public ShSettings()
        {
            ApplyDefaults();
        }

        // DataContract deserialization skips constructors; seed defaults first
        // so fields missing from an older settings file keep sane values
        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            ApplyDefaults();
        }

        // =================================================================
        // Section 1: defaults (match the original engine's defaults, with
        // the update checker removed and the splash off)
        // =================================================================
        public void ApplyDefaults()
        {
            QuickMenuCount = 5;
            ShowSplash = false;
            NotifyOnRestore = false;
            AskBeforeAutoRestore = false;
            MinimizeToTray = true;
            ShowHoverTips = true;

            RestoreDelaySeconds = 0;
            ZOrderMode = 1;
            FastRestore = true;
            FixOffscreen = true;
            EnhancedOffscreenFix = false;
            FixTaskbar = true;
            FixUnminimized = true;
            RestoreNewWindowsToLastPosition = true;
            RestoreClosedWindows = false;
            ReopenClosedApps = true;
            ShowDesktopWhenDisplayChanges = false;
            RestoreDesktopIcons = true;

            HotkeyQuickMenu = (int)(Keys.Control | Keys.Alt | Keys.L);
            HotkeySaveAs = 0;
            HotkeyUndo = 0;

            IgnoreProcesses = "";
            CaptureDelaySeconds = 0;
            CtrlMinimizeToTray = true;
            SwapOnAltActivate = true;
            DualPosition = true;
            WindowCommander = false;
        }

        // =================================================================
        // Section 2: load / save
        // =================================================================
        public static ShSettings Load(string folder)
        {
            string path = Path.Combine(folder, FileName);
            try
            {
                if (File.Exists(path))
                {
                    var ser = new DataContractJsonSerializer(typeof(ShSettings));
                    using (var fs = File.OpenRead(path))
                    {
                        var s = (ShSettings)ser.ReadObject(fs);
                        s.Clamp();
                        return s;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("settings load failed, using defaults: " + ex.Message);
            }
            return new ShSettings();
        }

        public void Save(string folder)
        {
            Clamp();
            string path = Path.Combine(folder, FileName);
            string tmp = path + ".tmp";
            var ser = new DataContractJsonSerializer(typeof(ShSettings));
            using (var ms = new MemoryStream())
            {
                using (var w = JsonReaderWriterFactory.CreateJsonWriter(ms, Encoding.UTF8, false, true, "  "))
                {
                    ser.WriteObject(w, this);
                }
                File.WriteAllBytes(tmp, ms.ToArray());
            }
            // atomic replace so a crash never leaves a half-written file
            if (File.Exists(path))
                File.Replace(tmp, path, null);
            else
                File.Move(tmp, path);
        }

        // keep values in usable ranges
        private void Clamp()
        {
            if (QuickMenuCount < 1) QuickMenuCount = 1;
            if (QuickMenuCount > 20) QuickMenuCount = 20;
            if (ZOrderMode < 0 || ZOrderMode > 2) ZOrderMode = 1;
            if (RestoreDelaySeconds < 0) RestoreDelaySeconds = 0;
            if (CaptureDelaySeconds < 0) CaptureDelaySeconds = 0;
            if (IgnoreProcesses == null) IgnoreProcesses = "";
        }

        // =================================================================
        // Section 3: hotkey text helpers ("Ctrl+Alt+L")
        // =================================================================
        public static string HotkeyText(int value)
        {
            if (value == 0)
                return "None";
            Keys k = (Keys)value;
            var sb = new StringBuilder();
            if ((k & Keys.Control) != 0) sb.Append("Ctrl+");
            if ((k & Keys.Alt) != 0) sb.Append("Alt+");
            if ((k & Keys.Shift) != 0) sb.Append("Shift+");
            Keys key = k & Keys.KeyCode;
            string name = key.ToString();
            if (key >= Keys.D0 && key <= Keys.D9)
                name = ((char)('0' + (key - Keys.D0))).ToString();
            sb.Append(name);
            return sb.ToString();
        }
    }
}
