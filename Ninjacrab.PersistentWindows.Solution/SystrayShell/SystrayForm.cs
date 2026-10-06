using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

using PersistentWindows.Common;
using PersistentWindows.Common.Diagnostics;

namespace PersistentWindows.SystrayShell
{
    // =====================================================================
    // ScreenHerder tray icon.
    //   Left-click  -> quick menu (recent layouts, Save Desktop As,
    //                  Undo Last Restore, All Layouts)
    //   Right-click -> Settings, Help, Exit
    // =====================================================================
    public partial class SystrayForm : Form
    {
        // kept for the engine callbacks in Program.cs
        public bool toggleIcon = false;
        public bool autoUpgrade = false;

        private readonly GlobalHotkeys hotkeys;
        public IconKeeper Icons { get; private set; }
        private SettingsForm settingsForm;
        private HelpForm helpForm;
        private bool dialogOpen;

        // NotifyIcon's own "show menu" routine sets the foreground window
        // correctly so the menu takes keyboard input and closes on click-away
        private static readonly MethodInfo showContextMenu =
            typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);

        public SystrayForm()
        {
            InitializeComponent();

            // create window handles now so engine threads can marshal calls
            // onto this UI thread (BeginInvoke needs a handle)
            CreateHandle();
            var menuHandle = contextMenuStripSysTray.Handle;

            quickMenu.Opening += (s, e) => BuildQuickMenu();
            quickMenu.Closed += (s, e) => notifyIconMain.ContextMenuStrip = contextMenuStripSysTray;
            quickMenu.SlotKeyPressed += HandleSlotKey;
            ApplyTips(Program.Settings);

            hotkeys = new GlobalHotkeys();
            ApplyHotkeys(Program.Settings);

            // desktop icons are restored along with windows
            Icons = new IconKeeper(this, Program.AppdataFolder);
            Icons.SetEnabled(Program.Settings.RestoreDesktopIcons);
        }

        protected override void SetVisibleCore(bool value)
        {
            // tray-only app: the host form never shows
            base.SetVisibleCore(false);
        }

        // =================================================================
        // Section 1: opening the quick menu (left-click or shortcut)
        // =================================================================
        private void IconMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                ShowQuickMenu();
        }

        public void ShowQuickMenu()
        {
            if (dialogOpen)
                return;
            notifyIconMain.ContextMenuStrip = quickMenu;
            if (showContextMenu != null)
                showContextMenu.Invoke(notifyIconMain, null);
            else
                quickMenu.Show(Cursor.Position);
        }

        // =================================================================
        // Section 2: building the quick menu each time it opens
        // =================================================================
        private void BuildQuickMenu()
        {
            quickMenu.Items.Clear();
            string key = Program.pwp.CurrentDisplayKey;

            // header: which monitor setup this is
            var header = new ToolStripLabel(LayoutStore.DescribeMonitors(key))
            {
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(6, 4, 6, 2)
            };
            quickMenu.Items.Add(header);

            // most recent layouts for the monitors connected right now
            var layouts = Program.Layouts.ForDisplay(key)
                .Where(l => Program.pwp.HasSnapshot(l.DisplayKey, l.Slot))
                .Take(Program.Settings.QuickMenuCount)
                .ToList();

            if (layouts.Count == 0)
            {
                quickMenu.Items.Add(new ToolStripMenuItem("No saved layouts for these monitors yet") { Enabled = false });
            }
            else
            {
                foreach (var l in layouts)
                {
                    var item = new ToolStripMenuItem(l.Name.Replace("&", "&&"))
                    {
                        ShortcutKeyDisplayString = char.ToUpperInvariant(l.SlotChar).ToString(),
                        ToolTipText = "Saved " + l.SavedAt.ToString("g")
                    };
                    var captured = l;
                    item.Click += (s, e) => RestoreLayout(captured);
                    quickMenu.Items.Add(item);
                }
            }

            quickMenu.Items.Add(new ToolStripSeparator());

            var saveAs = new ToolStripMenuItem("Save Desktop As…")
            {
                ShortcutKeyDisplayString = ShortcutText(Program.Settings.HotkeySaveAs)
            };
            saveAs.Font = new Font(saveAs.Font, FontStyle.Bold);
            saveAs.ToolTipText = "Name and save where every window and desktop icon is right now.";
            saveAs.Click += (s, e) => BeginInvoke((Action)SaveDesktopAs);
            quickMenu.Items.Add(saveAs);

            var undo = new ToolStripMenuItem("Undo Last Restore")
            {
                Enabled = Program.pwp.HasSnapshot(key, PersistentWindowProcessor.UndoSnapshotId),
                ShortcutKeyDisplayString = ShortcutText(Program.Settings.HotkeyUndo)
            };
            undo.Click += (s, e) => UndoLastRestore();
            undo.ToolTipText = "Put windows and icons back the way they were before the last layout you loaded.";
            quickMenu.Items.Add(undo);

            var all = new ToolStripMenuItem("All Layouts…");
            all.Click += (s, e) => BeginInvoke((Action)(() => OpenSettings(showLayouts: true)));
            all.ToolTipText = "See, load, rename or delete every saved layout, including ones for other monitors.";
            quickMenu.Items.Add(all);
        }

        private static string ShortcutText(int value)
        {
            return value == 0 ? "" : ShSettings.HotkeyText(value);
        }

        // =================================================================
        // Section 3: layout actions
        // =================================================================
        public void RestoreLayout(NamedLayout layout)
        {
            if (layout.DisplayKey != Program.pwp.CurrentDisplayKey)
            {
                Balloon("Different monitors", "\"" + layout.Name + "\" was saved on " + LayoutStore.DescribeMonitors(layout.DisplayKey) + ".");
                return;
            }
            Program.RestoreSnapshot(layout.Slot);
            Icons.RestoreForLayout(layout.DisplayKey, layout.Slot);
            Program.Layouts.Touch(layout);
            Log.Event("restored layout {0} (slot {1})", layout.Name, layout.Slot);
        }

        public void UndoLastRestore()
        {
            string key = Program.pwp.CurrentDisplayKey;
            if (!Program.pwp.HasSnapshot(key, PersistentWindowProcessor.UndoSnapshotId))
            {
                Balloon("Nothing to undo", "No layout has been restored on these monitors yet.");
                return;
            }
            Program.RestoreSnapshot(PersistentWindowProcessor.UndoSnapshotId);
            Icons.RestoreUndo(key);
        }

        public void SaveDesktopAs()
        {
            if (dialogOpen)
                return;
            string key = Program.pwp.CurrentDisplayKey;
            if (string.IsNullOrEmpty(key))
                return;

            string suggestion = "Layout " + (Program.Layouts.ForDisplay(key).Count + 1);
            string name;
            dialogOpen = true;
            try
            {
                using (var dlg = new NameLayoutDialog("Save Desktop As", "Name this window layout:", suggestion, LayoutStore.DescribeMonitors(key)))
                {
                    if (dlg.ShowDialog() != DialogResult.OK)
                        return;
                    name = dlg.LayoutName;
                }

                // same name on the same monitors: replace it after asking
                var existing = Program.Layouts.FindByName(key, name);
                int slot;
                if (existing != null)
                {
                    if (MessageBox.Show("Replace the existing layout \"" + existing.Name + "\" with the current desktop?",
                        "ScreenHerder", MessageBoxButtons.OKCancel, MessageBoxIcon.Question,
                        MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly) != DialogResult.OK)
                        return;
                    slot = existing.Slot;
                }
                else
                {
                    slot = Program.Layouts.AllocateSlot(key);
                    if (slot < 0)
                    {
                        MessageBox.Show("All 36 layout slots are in use for these monitors. Delete a layout under Settings > Layouts first.",
                            "ScreenHerder", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                            MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                        return;
                    }
                }

                SaveToSlot(key, slot, name);
            }
            finally
            {
                dialogOpen = false;
            }
        }

        private void SaveToSlot(string key, int slot, string name)
        {
            Program.CaptureSnapshot(slot, prompt: false);
            Icons.SaveForLayout(key, slot);
            Program.Layouts.Upsert(key, slot, name);
            Balloon("Layout saved", "\"" + name + "\" (key " + char.ToUpperInvariant(Program.SnapshotIdToChar(slot)) + ")");
        }

        // letter/digit pressed while the quick menu was open
        private void HandleSlotKey(char c, bool shift)
        {
            string key = Program.pwp.CurrentDisplayKey;
            int slot = Program.SnapshotCharToId(c);
            if (slot < 0 || slot > LayoutStore.MaxSlot || string.IsNullOrEmpty(key))
                return;

            var layout = Program.Layouts.FindBySlot(key, slot);
            if (shift)
            {
                string name = layout != null ? layout.Name : "Layout " + char.ToUpperInvariant(c);
                SaveToSlot(key, slot, name);
            }
            else if (layout != null && Program.pwp.HasSnapshot(key, slot))
            {
                RestoreLayout(layout);
            }
            else
            {
                Balloon("No layout on " + char.ToUpperInvariant(c), "Hold Shift and press " + char.ToUpperInvariant(c) + " in the menu to save one there.");
            }
        }

        // =================================================================
        // Section 4: windows (Settings, Help) and shortcuts
        // =================================================================
        public void OpenSettings(bool showLayouts = false)
        {
            if (settingsForm == null || settingsForm.IsDisposed)
            {
                settingsForm = new SettingsForm(Program.Settings);
                settingsForm.FormClosed += (s, e) => settingsForm = null;
                MinimizeToTray(settingsForm);
            }
            if (showLayouts)
                settingsForm.ShowLayoutsTab();
            settingsForm.Show();
            settingsForm.WindowState = FormWindowState.Normal;
            settingsForm.Activate();
        }

        public void OpenHelp()
        {
            if (helpForm == null || helpForm.IsDisposed)
            {
                helpForm = new HelpForm();
                helpForm.FormClosed += (s, e) => helpForm = null;
                MinimizeToTray(helpForm);
            }
            helpForm.Show();
            helpForm.Activate();
        }

        // hover tips on the tray menus follow the "Show tips" setting
        public void ApplyTips(ShSettings s)
        {
            quickMenu.ShowItemToolTips = s.ShowHoverTips;
            contextMenuStripSysTray.ShowItemToolTips = s.ShowHoverTips;
        }

        // with "Minimize to the tray" on, a minimized ScreenHerder window
        // leaves the taskbar; Settings / Help on the right-click menu bring it back
        private bool trayHintShown;
        private void MinimizeToTray(Form f)
        {
            f.Resize += (s, e) =>
            {
                if (f.WindowState != FormWindowState.Minimized || !Program.Settings.MinimizeToTray)
                    return;
                f.Hide();
                if (!trayHintShown)
                {
                    trayHintShown = true;
                    Balloon(f.Text + " is still open", "Right-click the sheep to bring it back.");
                }
            };
        }

        public void ApplyHotkeys(ShSettings s)
        {
            hotkeys.UnregisterAll();
            var failed = new List<string>();
            if (!hotkeys.Register(s.HotkeyQuickMenu, () => ShowQuickMenu()))
                failed.Add(ShSettings.HotkeyText(s.HotkeyQuickMenu));
            if (!hotkeys.Register(s.HotkeySaveAs, () => SaveDesktopAs()))
                failed.Add(ShSettings.HotkeyText(s.HotkeySaveAs));
            if (!hotkeys.Register(s.HotkeyUndo, () => UndoLastRestore()))
                failed.Add(ShSettings.HotkeyText(s.HotkeyUndo));
            if (failed.Count > 0)
                Balloon("Shortcut unavailable", string.Join(", ", failed) + " is already used by another program. Pick a different one in Settings > Shortcuts.");
        }

        public void Balloon(string title, string text)
        {
            if (Program.Gui)
                notifyIconMain.ShowBalloonTip(4000, title, text, ToolTipIcon.Info);
        }

        // =================================================================
        // Section 5: engine callbacks kept from the original tray form
        // =================================================================
        public void UpdateMenuEnable(bool enableRestoreFromDB, bool checkUpgrade)
        {
            // update checks are disabled in ScreenHerder; nothing to refresh
        }

        public void EnableSnapshotRestore(bool enable)
        {
            // the quick menu checks snapshot availability every time it opens
        }

        // =================================================================
        // Section 6: exit
        // =================================================================
        public void Exit()
        {
            var process = Process.GetCurrentProcess();
            process.PriorityClass = ProcessPriorityClass.High;

            Program.WriteDataDump();
            Log.Event("Session exit");

            hotkeys.Dispose();
            Icons.Dispose();
            notifyIconMain.Visible = false;

            Log.Exit();
            Program.Stop();
            Application.Exit();
        }
    }
}
