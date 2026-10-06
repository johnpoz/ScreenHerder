using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

using PersistentWindows.Common.Diagnostics;

namespace PersistentWindows.SystrayShell
{
    // =====================================================================
    // One desktop icon: its shell parsing name (full path, or a ::{CLSID}
    // for items like Recycle Bin) and its position on the desktop.
    // =====================================================================
    [DataContract]
    public class IconPos
    {
        [DataMember] public string Name;
        [DataMember] public int X;
        [DataMember] public int Y;
    }

    // =====================================================================
    // Reads and moves desktop icons through the desktop's shell view
    // (IShellWindows -> IShellBrowser -> IShellView -> IFolderView2), the
    // documented way to reach Explorer's desktop. Must run on an STA
    // thread (the app's UI thread is STA).
    // =====================================================================
    public static class DesktopIcons
    {
        // ---------------- Section 1: COM declarations ----------------
        private static readonly Guid CLSID_ShellWindows = new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39");
        private static readonly Guid SID_STopLevelBrowser = new Guid("4C96BE40-915C-11CF-99D3-00AA004AE837");
        private static readonly Guid IID_IShellBrowser = new Guid("000214E2-0000-0000-C000-000000000046");
        private static readonly Guid IID_IShellItem = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");

        private const int SWC_DESKTOP = 8;
        private const int SWFO_NEEDDISPATCH = 1;
        private const uint SVGIO_ALLVIEW = 2;
        private const uint SVSI_POSITIONITEM = 0x80;
        private const uint FWF_AUTOARRANGE = 0x1;
        private const uint SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IServiceProvider
        {
            [PreserveSig] int QueryService(ref Guid guidService, ref Guid riid, out IntPtr ppv);
        }

        // only QueryActiveShellView is called; earlier slots keep the vtable order
        [ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellBrowser
        {
            [PreserveSig] int GetWindow();
            [PreserveSig] int ContextSensitiveHelp();
            [PreserveSig] int InsertMenusSB();
            [PreserveSig] int SetMenuSB();
            [PreserveSig] int RemoveMenusSB();
            [PreserveSig] int SetStatusTextSB();
            [PreserveSig] int EnableModelessSB();
            [PreserveSig] int TranslateAcceleratorSB();
            [PreserveSig] int BrowseObject();
            [PreserveSig] int GetViewStateStream();
            [PreserveSig] int GetControlWindow();
            [PreserveSig] int SendControlMsg();
            [PreserveSig] int QueryActiveShellView(out IntPtr ppshv);
        }

        // IFolderView methods in order, then IFolderView2 up to GetItem
        [ComImport, Guid("1AF3A467-214F-4298-908E-06B03E0B39F9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFolderView2
        {
            // IFolderView
            [PreserveSig] int GetCurrentViewMode(out uint mode);
            [PreserveSig] int SetCurrentViewMode(uint mode);
            [PreserveSig] int GetFolder(ref Guid riid, out IntPtr ppv);
            [PreserveSig] int Item(int index, out IntPtr ppidl);
            [PreserveSig] int ItemCount(uint flags, out int count);
            [PreserveSig] int Items(uint flags, ref Guid riid, out IntPtr ppv);
            [PreserveSig] int GetSelectionMarkedItem(out int item);
            [PreserveSig] int GetFocusedItem(out int item);
            [PreserveSig] int GetItemPosition(IntPtr pidl, out POINT pt);
            [PreserveSig] int GetSpacing(ref POINT pt);
            [PreserveSig] int GetDefaultSpacing(out POINT pt);
            [PreserveSig] int GetAutoArrange();
            [PreserveSig] int SelectItem(int item, uint flags);
            [PreserveSig] int SelectAndPositionItems(uint cidl,
                [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl,
                [MarshalAs(UnmanagedType.LPArray)] POINT[] apt, uint flags);
            // IFolderView2
            [PreserveSig] int SetGroupBy();
            [PreserveSig] int GetGroupBy();
            [PreserveSig] int SetViewProperty();
            [PreserveSig] int GetViewProperty();
            [PreserveSig] int SetTileViewProperties();
            [PreserveSig] int SetExtendedTileViewProperties();
            [PreserveSig] int SetText();
            [PreserveSig] int SetCurrentFolderFlags(uint mask, uint flags);
            [PreserveSig] int GetCurrentFolderFlags(out uint flags);
            [PreserveSig] int GetSortColumnCount();
            [PreserveSig] int SetSortColumns();
            [PreserveSig] int GetSortColumns();
            [PreserveSig] int GetItem(int item, ref Guid riid, out IntPtr ppv);
        }

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            [PreserveSig] int BindToHandler();
            [PreserveSig] int GetParent();
            [PreserveSig] int GetDisplayName(uint sigdn, out IntPtr ppszName);
        }

        // ---------------- Section 2: find the desktop's folder view ----------------
        private static IFolderView2 GetDesktopView()
        {
            IntPtr pView = GetDesktopShellView();
            if (pView == IntPtr.Zero)
                return null;
            var view = Marshal.GetObjectForIUnknown(pView) as IFolderView2;
            Marshal.Release(pView);
            return view;
        }

        // only GetItemObject is called; earlier slots keep the vtable order
        [ComImport, Guid("000214E3-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellView
        {
            [PreserveSig] int GetWindow();
            [PreserveSig] int ContextSensitiveHelp();
            [PreserveSig] int TranslateAccelerator();
            [PreserveSig] int EnableModeless();
            [PreserveSig] int UIActivate();
            [PreserveSig] int Refresh();
            [PreserveSig] int CreateViewWindow();
            [PreserveSig] int DestroyViewWindow();
            [PreserveSig] int GetCurrentInfo();
            [PreserveSig] int AddPropertySheetPages();
            [PreserveSig] int SaveViewState();
            [PreserveSig] int SelectItem();
            [PreserveSig] int GetItemObject(uint uItem, ref Guid riid, out IntPtr ppv);
        }

        // -----------------------------------------------------------------
        // Start a program through Explorer, so it runs as the normal
        // (non-administrator) user even though ScreenHerder runs elevated.
        // Uses the desktop's Shell.Application object (IShellDispatch2).
        // -----------------------------------------------------------------
        public static bool ShellExecuteAsUser(string file, string args, string dir)
        {
            IntPtr pView = GetDesktopShellView();
            if (pView == IntPtr.Zero)
                return false;
            try
            {
                var view = (IShellView)Marshal.GetObjectForIUnknown(pView);
                Guid iidDispatch = new Guid("00020400-0000-0000-C000-000000000046");
                IntPtr pDisp;
                if (view.GetItemObject(0 /* SVGIO_BACKGROUND */, ref iidDispatch, out pDisp) != 0 || pDisp == IntPtr.Zero)
                    return false;
                object folderView = Marshal.GetObjectForIUnknown(pDisp);
                Marshal.Release(pDisp);
                object app = folderView.GetType().InvokeMember("Application", BindingFlags.GetProperty, null, folderView, null);
                app.GetType().InvokeMember("ShellExecute", BindingFlags.InvokeMethod, null, app,
                    new object[] { file, args ?? "", dir ?? "", "", 1 });
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("launch through Explorer failed for {0}: {1}", file, ex.Message);
                return false;
            }
            finally
            {
                Marshal.Release(pView);
            }
        }

        // desktop's active shell view (caller releases the pointer)
        private static IntPtr GetDesktopShellView()
        {
            object shellWindows = Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_ShellWindows));
            try
            {
                // FindWindowSW(&loc, &locRoot, SWC_DESKTOP, &hwnd, SWFO_NEEDDISPATCH)
                object[] args = { 0, null, SWC_DESKTOP, 0, SWFO_NEEDDISPATCH };
                var mods = new ParameterModifier(5);
                mods[0] = true; mods[1] = true; mods[3] = true;
                object disp = shellWindows.GetType().InvokeMember("FindWindowSW",
                    BindingFlags.InvokeMethod, null, shellWindows, args, new[] { mods }, null, null);
                if (disp == null)
                    return IntPtr.Zero;

                var sp = (IServiceProvider)disp;
                Guid sid = SID_STopLevelBrowser, iid = IID_IShellBrowser;
                IntPtr pBrowser;
                if (sp.QueryService(ref sid, ref iid, out pBrowser) != 0 || pBrowser == IntPtr.Zero)
                    return IntPtr.Zero;
                var browser = (IShellBrowser)Marshal.GetObjectForIUnknown(pBrowser);
                Marshal.Release(pBrowser);

                IntPtr pView;
                if (browser.QueryActiveShellView(out pView) != 0)
                    return IntPtr.Zero;
                return pView;
            }
            finally
            {
                Marshal.ReleaseComObject(shellWindows);
            }
        }

        private static string ItemName(IFolderView2 view, int index)
        {
            Guid iid = IID_IShellItem;
            IntPtr pItem;
            if (view.GetItem(index, ref iid, out pItem) != 0 || pItem == IntPtr.Zero)
                return null;
            try
            {
                var item = (IShellItem)Marshal.GetObjectForIUnknown(pItem);
                IntPtr psz;
                if (item.GetDisplayName(SIGDN_DESKTOPABSOLUTEPARSING, out psz) != 0 || psz == IntPtr.Zero)
                    return null;
                string name = Marshal.PtrToStringUni(psz);
                Marshal.FreeCoTaskMem(psz);
                return name;
            }
            finally
            {
                Marshal.Release(pItem);
            }
        }

        // ---------------- Section 3: read every icon's position ----------------
        public static List<IconPos> Capture()
        {
            var result = new List<IconPos>();
            var view = GetDesktopView();
            if (view == null)
                return null;
            try
            {
                int count;
                if (view.ItemCount(SVGIO_ALLVIEW, out count) != 0)
                    return null;
                for (int i = 0; i < count; i++)
                {
                    IntPtr pidl;
                    if (view.Item(i, out pidl) != 0 || pidl == IntPtr.Zero)
                        continue;
                    try
                    {
                        POINT pt;
                        string name = ItemName(view, i);
                        if (name != null && view.GetItemPosition(pidl, out pt) == 0)
                            result.Add(new IconPos { Name = name, X = pt.X, Y = pt.Y });
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(pidl);
                    }
                }
            }
            finally
            {
                Marshal.ReleaseComObject(view);
            }
            return result;
        }

        // ---------------- Section 4: put icons back ----------------
        // Icons that still exist go to their saved spots. Icons deleted
        // since are skipped (their spot stays empty). Icons added since
        // stay where they are. Auto-arrange is turned off, because with it
        // on Windows packs icons together and nothing can hold a position.
        public static int Restore(List<IconPos> saved)
        {
            if (saved == null || saved.Count == 0)
                return 0;
            var view = GetDesktopView();
            if (view == null)
                return 0;
            var pidls = new List<IntPtr>();
            try
            {
                var wanted = new Dictionary<string, IconPos>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in saved)
                    wanted[p.Name] = p;

                uint flags;
                if (view.GetCurrentFolderFlags(out flags) == 0 && (flags & FWF_AUTOARRANGE) != 0)
                {
                    view.SetCurrentFolderFlags(FWF_AUTOARRANGE, 0);
                    Log.Event("desktop auto-arrange turned off so icon positions can be restored");
                }

                int count;
                if (view.ItemCount(SVGIO_ALLVIEW, out count) != 0)
                    return 0;

                var points = new List<POINT>();
                for (int i = 0; i < count; i++)
                {
                    string name = ItemName(view, i);
                    IconPos p;
                    if (name == null || !wanted.TryGetValue(name, out p))
                        continue;
                    IntPtr pidl;
                    if (view.Item(i, out pidl) != 0 || pidl == IntPtr.Zero)
                        continue;
                    POINT cur;
                    if (view.GetItemPosition(pidl, out cur) == 0 && cur.X == p.X && cur.Y == p.Y)
                    {
                        Marshal.FreeCoTaskMem(pidl);   // already in place
                        continue;
                    }
                    pidls.Add(pidl);
                    points.Add(new POINT { X = p.X, Y = p.Y });
                }

                if (pidls.Count > 0)
                    view.SelectAndPositionItems((uint)pidls.Count, pidls.ToArray(), points.ToArray(), SVSI_POSITIONITEM);
                return pidls.Count;
            }
            finally
            {
                foreach (var pidl in pidls)
                    Marshal.FreeCoTaskMem(pidl);
                Marshal.ReleaseComObject(view);
            }
        }

        public static bool Same(List<IconPos> a, List<IconPos> b)
        {
            if (a == null || b == null || a.Count != b.Count)
                return false;
            var map = b.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var p in a)
            {
                IconPos q;
                if (!map.TryGetValue(p.Name, out q) || q.X != p.X || q.Y != p.Y)
                    return false;
            }
            return true;
        }
    }

    // =====================================================================
    // Saved desktop icon arrangements (icons.json), keyed by monitor set
    // plus a tag: "live" (kept current automatically), "undo", or a
    // layout slot letter.
    // =====================================================================
    public class IconStore
    {
        public const string FileName = "icons.json";
        public const string Live = "live";
        public const string Undo = "undo";

        private readonly string path;
        private Dictionary<string, List<IconPos>> sets = new Dictionary<string, List<IconPos>>();

        public IconStore(string folder)
        {
            path = Path.Combine(folder, FileName);
            try
            {
                if (File.Exists(path))
                {
                    var ser = new DataContractJsonSerializer(typeof(Dictionary<string, List<IconPos>>),
                        new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
                    using (var fs = File.OpenRead(path))
                        sets = (Dictionary<string, List<IconPos>>)ser.ReadObject(fs) ?? sets;
                }
            }
            catch (Exception ex)
            {
                Log.Error("icons load failed: " + ex.Message);
            }
        }

        private static string Key(string displayKey, string tag)
        {
            return displayKey + "|" + tag;
        }

        public static string SlotTag(int slot)
        {
            return "slot" + slot;
        }

        public List<IconPos> Get(string displayKey, string tag)
        {
            List<IconPos> v;
            return sets.TryGetValue(Key(displayKey, tag), out v) ? v : null;
        }

        public void Put(string displayKey, string tag, List<IconPos> icons)
        {
            if (string.IsNullOrEmpty(displayKey) || icons == null)
                return;
            sets[Key(displayKey, tag)] = icons;
            Save();
        }

        public void Remove(string displayKey, string tag)
        {
            if (sets.Remove(Key(displayKey, tag)))
                Save();
        }

        private void Save()
        {
            try
            {
                string tmp = path + ".tmp";
                var ser = new DataContractJsonSerializer(typeof(Dictionary<string, List<IconPos>>),
                    new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
                using (var ms = new MemoryStream())
                {
                    ser.WriteObject(ms, sets);
                    File.WriteAllBytes(tmp, ms.ToArray());
                }
                if (File.Exists(path))
                    File.Replace(tmp, path, null);
                else
                    File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                Log.Error("icons save failed: " + ex.Message);
            }
        }
    }
}
