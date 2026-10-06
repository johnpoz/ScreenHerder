using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using PersistentWindows.Common.Diagnostics;

namespace PersistentWindows.SystrayShell
{
    // =====================================================================
    // Global hotkeys (system-wide shortcuts) for ScreenHerder actions.
    // A hidden message-only window receives WM_HOTKEY.
    // =====================================================================
    public class GlobalHotkeys : NativeWindow, IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_NOREPEAT = 0x4000;
        private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private readonly Dictionary<int, Action> actions = new Dictionary<int, Action>();
        private int nextId = 1;

        public GlobalHotkeys()
        {
            CreateHandle(new CreateParams { Parent = HWND_MESSAGE });
        }

        // -----------------------------------------------------------------
        // Section 1: register one shortcut; returns false if another app
        // already owns it
        // -----------------------------------------------------------------
        public bool Register(int keysValue, Action action)
        {
            if (keysValue == 0)
                return true;
            Keys k = (Keys)keysValue;
            uint mods = MOD_NOREPEAT;
            if ((k & Keys.Control) != 0) mods |= MOD_CONTROL;
            if ((k & Keys.Alt) != 0) mods |= MOD_ALT;
            if ((k & Keys.Shift) != 0) mods |= MOD_SHIFT;
            uint vk = (uint)(k & Keys.KeyCode);

            int id = nextId++;
            if (!RegisterHotKey(Handle, id, mods, vk))
            {
                Log.Error("hotkey {0} unavailable (error {1})", ShSettings.HotkeyText(keysValue), Marshal.GetLastWin32Error());
                return false;
            }
            actions[id] = action;
            return true;
        }

        public void UnregisterAll()
        {
            foreach (var id in actions.Keys)
                UnregisterHotKey(Handle, id);
            actions.Clear();
        }

        // -----------------------------------------------------------------
        // Section 2: dispatch
        // -----------------------------------------------------------------
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                Action a;
                if (actions.TryGetValue(m.WParam.ToInt32(), out a))
                {
                    try { a(); }
                    catch (Exception ex) { Log.Error(ex.ToString()); }
                }
                return;
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            UnregisterAll();
            DestroyHandle();
        }
    }

    // =====================================================================
    // Left-click quick menu. While it is open, a letter or digit key
    // restores that layout slot, and Shift + letter/digit saves to it.
    // =====================================================================
    public class QuickMenuStrip : ContextMenuStrip
    {
        // char = slot character ('a'..'z', '0'..'9'); bool = Shift held
        public event Action<char, bool> SlotKeyPressed;

        public QuickMenuStrip(System.ComponentModel.IContainer container) : base(container)
        {
            ShowImageMargin = false;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            bool ctrlOrAlt = (keyData & (Keys.Control | Keys.Alt)) != 0;
            bool shift = (keyData & Keys.Shift) != 0;

            char c = '\0';
            if (key >= Keys.A && key <= Keys.Z)
                c = (char)('a' + (key - Keys.A));
            else if (key >= Keys.D0 && key <= Keys.D9)
                c = (char)('0' + (key - Keys.D0));
            else if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
                c = (char)('0' + (key - Keys.NumPad0));

            if (c != '\0' && !ctrlOrAlt && SlotKeyPressed != null)
            {
                Close(ToolStripDropDownCloseReason.Keyboard);
                SlotKeyPressed(c, shift);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
