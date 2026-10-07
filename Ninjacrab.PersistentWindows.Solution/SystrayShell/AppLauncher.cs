using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;

using Microsoft.Win32;
using PersistentWindows.Common.Diagnostics;

namespace PersistentWindows.SystrayShell
{
    // =====================================================================
    // One window of a saved layout, with what's needed to start its
    // program again if it isn't running when the layout is loaded.
    // =====================================================================
    [DataContract]
    public class AppRecord
    {
        [DataMember] public string Kind;        // "exe", "store" (Microsoft Store app) or "folder" (Explorer window)
        [DataMember] public string ExePath;     // program file (exe kind)
        [DataMember] public string Arguments;   // what it was started with, minus the program itself
        [DataMember] public string Aumid;       // Store app id (store kind)
        [DataMember] public string FolderPath;  // folder shown (folder kind)
        [DataMember] public string Title;       // window title when saved, for messages
        [DataMember] public int ShowCmd;        // normal / minimized / maximized
        [DataMember] public int Left, Top, Right, Bottom;   // normal (restored) position

        // identity used to decide "is this app already running?"
        public string Identity
        {
            get
            {
                if (Kind == "store") return "store:" + (Aumid ?? "").ToLowerInvariant();
                if (Kind == "folder") return "folder:" + (FolderPath ?? "").TrimEnd('\\').ToLowerInvariant();
                return "exe:" + (ExePath ?? "").ToLowerInvariant();
            }
        }

        public string DisplayName
        {
            get
            {
                if (Kind == "folder") return Path.GetFileName((FolderPath ?? "").TrimEnd('\\')) is string f && f.Length > 0 ? f : FolderPath;
                if (Kind == "store") return string.IsNullOrEmpty(Title) ? Aumid : Title;
                return Path.GetFileNameWithoutExtension(ExePath ?? "");
            }
        }
    }

    // =====================================================================
    // Capture the programs behind the windows on screen, and reopen the
    // ones that aren't running when a layout is loaded.
    //  - ScreenHerder runs as the normal user (no administrator rights),
    //    so programs are started the ordinary way and run as you.
    //  - A program whose file no longer exists, or a Store app that's no
    //    longer installed, is skipped and its spot left empty.
    //  - An app with several windows is launched once; the windows it
    //    opens are placed in the saved spots in order.
    //  - Only used when loading a named layout, never after a monitor change.
    // =====================================================================
    public static class AppLauncher
    {
        private const int WaitForWindowMs = 20000;
        private const int PollMs = 500;

        // ---------------- Section 1: Win32 declarations ----------------
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int index);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder s, int n);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder s, int n);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT wp);
        [DllImport("user32.dll")] private static extern bool SetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT wp);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out int value, int size);
        [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder name, ref int size);
        [DllImport("shell32.dll")] private static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid iid, out IPropertyStore store);
        [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PROPVARIANT pv);

        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPLACEMENT
        {
            public int length, flags, showCmd;
            public POINT ptMin, ptMax;
            public RECT rcNormal;
        }
        [StructLayout(LayoutKind.Sequential, Pack = 4)] private struct PROPERTYKEY { public Guid fmtid; public uint pid; }
        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct PROPVARIANT
        {
            [FieldOffset(0)] public ushort vt;
            [FieldOffset(8)] public IntPtr ptr;
        }
        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            [PreserveSig] int GetCount(out uint count);
            [PreserveSig] int GetAt(uint i, out PROPERTYKEY key);
            [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        }

        private const uint GW_OWNER = 4;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x80;
        private const int DWMWA_CLOAKED = 14;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        private static readonly HashSet<string> SkipClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow" };

        // ---------------- Section 2: windows on screen right now ----------------
        private class LiveWindow
        {
            public IntPtr Hwnd;
            public uint Pid;
            public string Class, Title, ExePath, Aumid, FolderPath;
            public AppRecord AsRecord()
            {
                var r = new AppRecord { Title = Title };
                if (Class == "CabinetWClass" && !string.IsNullOrEmpty(FolderPath)) { r.Kind = "folder"; r.FolderPath = FolderPath; }
                else if (IsStoreWindow) { r.Kind = "store"; r.Aumid = Aumid; }
                else { r.Kind = "exe"; r.ExePath = ExePath; }
                return r;
            }
            public bool IsStoreWindow
            {
                get
                {
                    if (string.IsNullOrEmpty(Aumid) || !Aumid.Contains("!"))
                        return false;
                    string exe = ExePath ?? "";
                    return exe.EndsWith("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase)
                        || exe.IndexOf("\\WindowsApps\\", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
        }

        private static List<LiveWindow> ListWindows()
        {
            uint self = (uint)Process.GetCurrentProcess().Id;
            var folders = ExplorerFolders();
            var list = new List<LiveWindow>();
            EnumWindows((h, p) =>
            {
                try
                {
                    if (!IsWindowVisible(h) || GetWindow(h, GW_OWNER) != IntPtr.Zero)
                        return true;
                    if ((GetWindowLong(h, GWL_EXSTYLE) & WS_EX_TOOLWINDOW) != 0)
                        return true;
                    int cloaked;
                    if (DwmGetWindowAttribute(h, DWMWA_CLOAKED, out cloaked, 4) == 0 && cloaked != 0)
                        return true;
                    var sb = new StringBuilder(256);
                    GetClassName(h, sb, 256);
                    string cls = sb.ToString();
                    if (SkipClasses.Contains(cls))
                        return true;
                    sb.Clear();
                    GetWindowText(h, sb, 256);
                    string title = sb.ToString();
                    if (title.Length == 0)
                        return true;
                    uint pid;
                    GetWindowThreadProcessId(h, out pid);
                    if (pid == self)
                        return true;

                    var w = new LiveWindow { Hwnd = h, Pid = pid, Class = cls, Title = title, ExePath = ImagePath(pid), Aumid = WindowAumid(h) };
                    string folder;
                    if (cls == "CabinetWClass" && folders.TryGetValue(h, out folder))
                        w.FolderPath = folder;
                    bool isExplorer = (w.ExePath ?? "").EndsWith("\\explorer.exe", StringComparison.OrdinalIgnoreCase);
                    if (isExplorer && w.FolderPath == null)
                        return true;   // other Explorer-owned windows aren't apps
                    if (w.ExePath == null && !w.IsStoreWindow)
                        return true;
                    list.Add(w);
                }
                catch (Exception ex)
                {
                    Log.Error("window scan: " + ex.Message);
                }
                return true;
            }, IntPtr.Zero);
            return list;
        }

        private static string ImagePath(uint pid)
        {
            IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero)
                return null;
            try
            {
                var sb = new StringBuilder(1024);
                int size = sb.Capacity;
                return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString() : null;
            }
            finally
            {
                CloseHandle(h);
            }
        }

        // Store app id of a window (PKEY_AppUserModel_ID)
        private static string WindowAumid(IntPtr hwnd)
        {
            IPropertyStore store = null;
            try
            {
                Guid iid = new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
                if (SHGetPropertyStoreForWindow(hwnd, ref iid, out store) != 0 || store == null)
                    return null;
                var key = new PROPERTYKEY { fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), pid = 5 };
                PROPVARIANT pv;
                if (store.GetValue(ref key, out pv) != 0)
                    return null;
                try
                {
                    return pv.vt == 31 /* VT_LPWSTR */ ? Marshal.PtrToStringUni(pv.ptr) : null;
                }
                finally
                {
                    PropVariantClear(ref pv);
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                if (store != null)
                    Marshal.ReleaseComObject(store);
            }
        }

        // folder shown by each open Explorer window
        private static Dictionary<IntPtr, string> ExplorerFolders()
        {
            var result = new Dictionary<IntPtr, string>();
            object shellWindows = null;
            try
            {
                shellWindows = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39")));
                dynamic sw = shellWindows;
                int count = sw.Count;
                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        dynamic w = sw.Item(i);
                        if (w == null)
                            continue;
                        long hwnd = w.HWND;
                        string path = w.Document.Folder.Self.Path;
                        if (!string.IsNullOrEmpty(path))
                            result[new IntPtr(hwnd)] = path;
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Log.Error("explorer folder scan: " + ex.Message);
            }
            finally
            {
                if (shellWindows != null)
                    Marshal.ReleaseComObject(shellWindows);
            }
            return result;
        }

        // what one app window's program was started with, minus the program itself
        private static string ArgumentsOf(uint pid)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + pid))
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        string cmd = mo["CommandLine"] as string;
                        if (cmd != null)
                            return StripProgram(cmd);
                    }
            }
            catch (Exception ex)
            {
                Log.Error("command line read: " + ex.Message);
            }
            return null;
        }

        private static string StripProgram(string cmd)
        {
            cmd = cmd.Trim();
            int end;
            if (cmd.StartsWith("\""))
            {
                end = cmd.IndexOf('"', 1);
                end = end < 0 ? cmd.Length : end + 1;
            }
            else
            {
                end = cmd.IndexOf(' ');
                if (end < 0) end = cmd.Length;
            }
            return cmd.Substring(end).Trim();
        }

        // ---------------- Section 3: capture when a layout is saved ----------------
        public static List<AppRecord> Capture()
        {
            var records = new List<AppRecord>();
            try
            {
                foreach (var w in ListWindows())
                {
                    var r = w.AsRecord();
                    if (r.Kind == "exe")
                        r.Arguments = ArgumentsOf(w.Pid);
                    var wp = new WINDOWPLACEMENT { length = Marshal.SizeOf(typeof(WINDOWPLACEMENT)) };
                    if (GetWindowPlacement(w.Hwnd, ref wp))
                    {
                        r.ShowCmd = wp.showCmd;
                        r.Left = wp.rcNormal.Left; r.Top = wp.rcNormal.Top;
                        r.Right = wp.rcNormal.Right; r.Bottom = wp.rcNormal.Bottom;
                    }
                    records.Add(r);
                }
            }
            catch (Exception ex)
            {
                Log.Error("app capture failed: " + ex.Message);
            }
            return records;
        }

        // ---------------- Section 4: reopen what's missing ----------------
        // Launches happen on the calling (UI, STA) thread; waiting for the
        // new windows and placing them happens in the background.
        // onDone receives the names of apps that couldn't be reopened.
        public static void ReopenMissing(List<AppRecord> saved, Action<List<string>> onDone)
        {
            if (saved == null || saved.Count == 0)
                return;

            var live = ListWindows();
            var running = new HashSet<string>(live.Select(w => w.AsRecord().Identity));
            var before = new HashSet<IntPtr>(live.Select(w => w.Hwnd));

            var skipped = new List<string>();
            var waiting = new List<KeyValuePair<string, List<AppRecord>>>();
            foreach (var group in saved.GroupBy(r => r.Identity))
            {
                if (running.Contains(group.Key))
                    continue;   // already open; the window restore handles it
                var first = group.First();
                if (!IsInstalled(first))
                {
                    skipped.Add(first.DisplayName + " (no longer installed)");
                    continue;
                }
                if (!Launch(first))
                {
                    skipped.Add(first.DisplayName);
                    continue;
                }
                Log.Event("reopened {0} for a layout", first.DisplayName);
                waiting.Add(new KeyValuePair<string, List<AppRecord>>(group.Key, group.ToList()));
            }

            if (waiting.Count == 0)
            {
                if (skipped.Count > 0)
                    onDone(skipped);
                return;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                var placed = new HashSet<IntPtr>(before);
                var deadline = DateTime.Now.AddMilliseconds(WaitForWindowMs);
                var pending = waiting.ToDictionary(k => k.Key, k => new Queue<AppRecord>(k.Value));
                while (pending.Count > 0 && DateTime.Now < deadline)
                {
                    Thread.Sleep(PollMs);
                    List<LiveWindow> now;
                    try { now = ListWindows(); } catch { continue; }
                    foreach (var w in now)
                    {
                        if (placed.Contains(w.Hwnd))
                            continue;
                        string id = w.AsRecord().Identity;
                        Queue<AppRecord> q;
                        if (!pending.TryGetValue(id, out q))
                            continue;
                        placed.Add(w.Hwnd);
                        var r = q.Dequeue();
                        Place(w.Hwnd, r);
                        // apps sometimes move themselves right after opening; place once more
                        var hwnd = w.Hwnd;
                        ThreadPool.QueueUserWorkItem(__ => { Thread.Sleep(2000); Place(hwnd, r); });
                        if (q.Count == 0)
                            pending.Remove(id);
                    }
                }
                // anything still pending only opened some (or none) of its windows; that's expected
                if (skipped.Count > 0)
                    onDone(skipped);
            });
        }

        private static bool IsInstalled(AppRecord r)
        {
            switch (r.Kind)
            {
                case "folder":
                    return Directory.Exists(r.FolderPath);
                case "store":
                    {
                        // package family = everything before "!" in the app id
                        string family = (r.Aumid ?? "").Split('!')[0];
                        if (family.Length == 0)
                            return false;
                        using (var k = Registry.CurrentUser.OpenSubKey(
                            @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Families\" + family))
                            return k != null && k.SubKeyCount > 0;
                    }
                default:
                    return !string.IsNullOrEmpty(r.ExePath) && File.Exists(r.ExePath);
            }
        }

        private static bool Launch(AppRecord r)
        {
            try
            {
                ProcessStartInfo psi;
                switch (r.Kind)
                {
                    case "folder":
                        psi = new ProcessStartInfo("explorer.exe", "\"" + r.FolderPath + "\"");
                        break;
                    case "store":
                        psi = new ProcessStartInfo("explorer.exe", "shell:AppsFolder\\" + r.Aumid);
                        break;
                    default:
                        psi = new ProcessStartInfo(r.ExePath, r.Arguments ?? "")
                        {
                            WorkingDirectory = Path.GetDirectoryName(r.ExePath)
                        };
                        break;
                }
                psi.UseShellExecute = true;
                Process.Start(psi)?.Dispose();
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("couldn't start {0}: {1}", r.DisplayName, ex.Message);
                return false;
            }
        }

        private static void Place(IntPtr hwnd, AppRecord r)
        {
            try
            {
                if (r.Right <= r.Left || r.Bottom <= r.Top)
                    return;
                var wp = new WINDOWPLACEMENT
                {
                    length = Marshal.SizeOf(typeof(WINDOWPLACEMENT)),
                    showCmd = r.ShowCmd == 0 ? 1 : r.ShowCmd,
                    rcNormal = new RECT { Left = r.Left, Top = r.Top, Right = r.Right, Bottom = r.Bottom }
                };
                SetWindowPlacement(hwnd, ref wp);
            }
            catch (Exception ex)
            {
                Log.Error("placing reopened window failed: " + ex.Message);
            }
        }
    }
}
