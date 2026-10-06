using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

using PersistentWindows.Common;

namespace PersistentWindows.SystrayShell
{
    // =====================================================================
    // Text box that records a keyboard shortcut when pressed.
    // Backspace or Delete clears it.
    // =====================================================================
    public class HotkeyBox : TextBox
    {
        private int value;
        public int Value
        {
            get { return value; }
            set { this.value = value; Text = ShSettings.HotkeyText(value); }
        }

        public HotkeyBox()
        {
            ReadOnly = true;
            BackColor = SystemColors.Window;
            Width = 200;
            ShortcutsEnabled = false;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            e.Handled = true;
            Keys key = e.KeyCode;
            if (key == Keys.Back || key == Keys.Delete)
            {
                Value = 0;
                return;
            }
            // wait for a real key, not just a modifier
            if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu || key == Keys.LWin || key == Keys.RWin)
                return;
            // require Ctrl or Alt so a shortcut never steals normal typing
            if (!e.Control && !e.Alt)
            {
                Text = "Use Ctrl or Alt with a key";
                return;
            }
            Value = (int)e.KeyData;
        }
    }

    // =====================================================================
    // ScreenHerder Settings window
    // =====================================================================
    public class SettingsForm : Form
    {
        private readonly ShSettings s;
        private TabControl tabs;
        private TabPage layoutsPage;

        // General
        private NumericUpDown quickCount;
        private CheckBox showSplash, notifyRestore, askBeforeRestore, autoStart, minimizeToTray, hoverTips;
        private bool autoStartWas;

        // hover tips: appear after the pointer rests on an option for 3 seconds
        private readonly ToolTip tips = new ToolTip();
        // Restore
        private NumericUpDown restoreDelay;
        private ComboBox zorderMode;
        private CheckBox fastRestore, fixOffscreen, enhancedOffscreen, fixTaskbar, fixUnminimized,
                         restoreNewWindows, restoreClosed, showDesktop, restoreIcons;
        // Shortcuts
        private HotkeyBox hkQuick, hkSaveAs, hkUndo;
        // Layouts
        private ListView layoutList;
        private Button loadBtn, renameBtn, deleteBtn;
        // Advanced
        private TextBox ignoreProcs;
        private NumericUpDown captureDelay;
        private CheckBox ctrlMinimize, swapAlt, dualPos, windowCommander;

        public SettingsForm(ShSettings current)
        {
            s = current;

            // ---------------- Section 1: window chrome ----------------
            Text = "ScreenHerder Settings";
            Icon = Program.IdleIcon;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            Font = SystemFonts.MessageBoxFont;
            ClientSize = new Size(720, 520);
            MinimumSize = new Size(640, 460);
            ShowInTaskbar = true;

            tips.AutomaticDelay = 3000;   // sets all delays; then override the ones that matter
            tips.InitialDelay = 3000;
            tips.ReshowDelay = 500;
            tips.AutoPopDelay = 30000;
            tips.ShowAlways = true;
            tips.Active = s.ShowHoverTips;

            tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 4) };
            tabs.TabPages.Add(BuildGeneral());
            tabs.TabPages.Add(BuildRestore());
            tabs.TabPages.Add(BuildShortcuts());
            tabs.TabPages.Add(BuildLayouts());
            tabs.TabPages.Add(BuildAdvanced());

            // ---------------- Section 2: bottom buttons ----------------
            var save = new Button { Text = "Save", AutoSize = true, DialogResult = DialogResult.None };
            var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            var defaults = new Button { Text = "Restore Defaults", AutoSize = true };
            save.Click += (o, e) => SaveAndClose();
            tips.SetToolTip(save, "Keep these settings. Changes to how windows are restored make ScreenHerder restart itself, which takes a couple of seconds.");
            tips.SetToolTip(cancel, "Close without changing anything. Layout changes made on the Layouts tab are already saved.");
            tips.SetToolTip(defaults, "Put every option on every tab back to how it came. Your saved layouts are not touched. Nothing changes until you click Save.");
            defaults.Click += (o, e) =>
            {
                if (MessageBox.Show(this, "Reset every setting on every tab to its default? Your saved layouts are not affected.",
                    "ScreenHerder", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
                {
                    var d = new ShSettings();
                    LoadFrom(d);
                    if (autoStart.Enabled)
                        autoStart.Checked = true;
                }
            };

            var bottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Padding = new Padding(8)
            };
            bottom.Controls.Add(cancel);
            bottom.Controls.Add(save);
            bottom.Controls.Add(new Label { Width = 24 });
            bottom.Controls.Add(defaults);

            Controls.Add(tabs);
            Controls.Add(bottom);
            AcceptButton = save;
            CancelButton = cancel;

            LoadFrom(s);
            RefreshLayouts();

            // start-at-sign-in state lives in Task Scheduler, not settings.json
            autoStartWas = AutoStart.IsEnabled();
            autoStart.Checked = autoStartWas;
            autoStart.Enabled = AutoStart.CanManage;
            Shown += (o, e) => Activate();
        }

        // =================================================================
        // Section 3: page builders
        // =================================================================
        private static TableLayoutPanel Page(TabPage page)
        {
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                AutoScroll = true,
                Padding = new Padding(14)
            };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            page.Controls.Add(t);
            return t;
        }

        private void Row(TableLayoutPanel t, string label, Control c, string hint = null, string tip = null)
        {
            var l = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 12, 6) };
            t.Controls.Add(l);
            c.Margin = new Padding(0, 3, 0, 3);
            t.Controls.Add(c);
            Tip(tip ?? hint, l, c);
            if (hint != null)
                Hint(t, hint);
        }

        private void Tip(string text, params Control[] controls)
        {
            if (string.IsNullOrEmpty(text))
                return;
            foreach (var c in controls)
                tips.SetToolTip(c, text);
        }

        private CheckBox Check(TableLayoutPanel t, string text, string hint = null, string tip = null)
        {
            var cb = new CheckBox { Text = text, AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
            t.Controls.Add(cb);
            t.SetColumnSpan(cb, 2);
            Tip(tip ?? hint, cb);
            if (hint != null)
                Hint(t, hint);
            return cb;
        }

        private static void Hint(TableLayoutPanel t, string text)
        {
            var l = new Label
            {
                Text = text,
                AutoSize = true,
                MaximumSize = new Size(620, 0),
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(18, 0, 0, 8)
            };
            t.Controls.Add(l);
            t.SetColumnSpan(l, 2);
        }

        private static void Heading(TableLayoutPanel t, string text)
        {
            var l = new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
                Margin = new Padding(0, 10, 0, 4)
            };
            t.Controls.Add(l);
            t.SetColumnSpan(l, 2);
        }

        private static NumericUpDown Number(decimal min, decimal max, int decimals, decimal step)
        {
            return new NumericUpDown { Minimum = min, Maximum = max, DecimalPlaces = decimals, Increment = step, Width = 80 };
        }

        private TabPage BuildGeneral()
        {
            var page = new TabPage("General");
            var t = Page(page);
            Heading(t, "Quick Menu");
            quickCount = Number(1, 20, 0, 1);
            Row(t, "Layouts shown on left-click:", quickCount, tip: "Pick 1 to 20. The left-click menu lists this many of your most recently used layouts for the monitors you have plugged in right now. Every layout is always available under All Layouts.", hint:
                "How many of your most recently used layouts appear at the top of the left-click menu. Only layouts saved on the monitors you have plugged in right now are listed.");
            Heading(t, "Startup And Notifications");
            autoStart = Check(t, "Start ScreenHerder when I sign in", null,
                "Starts ScreenHerder automatically every time you sign in to Windows, with the rights it needs to move every window. Turn this off if you'd rather start it yourself from the Start menu. Changing it asks Windows for permission, because it edits a scheduled task.");
            showSplash = Check(t, "Show a splash screen when ScreenHerder starts", null,
                "Shows a small ScreenHerder banner for a moment when the app starts, so you know it's running.");
            notifyRestore = Check(t, "Show a notification when windows are restored after a monitor change", null,
                "Pops up a short notice near the clock each time ScreenHerder puts your windows back after you plug in or unplug a monitor.");
            askBeforeRestore = Check(t, "Ask before restoring after a monitor change",
                "Pauses and shows a message first, so you can stop the automatic restore.",
                "Instead of putting windows back right away after a monitor change, ScreenHerder asks first. Useful if you sometimes want to keep the new arrangement.");
            Heading(t, "Windows And Tips");
            minimizeToTray = Check(t, "Minimize ScreenHerder's windows to the tray", null,
                "When you minimize the Settings or Help window, it disappears into the ScreenHerder icon by the clock instead of sitting on the taskbar. Right-click the sheep and choose Settings or Help to bring it back.");
            hoverTips = Check(t, "Show tips when I hover over an option", null,
                "Shows an explanation like this one when the pointer rests on an option for 3 seconds. Turn it off once you know your way around.");
            hoverTips.CheckedChanged += (o, e) => tips.Active = hoverTips.Checked;
            return page;
        }

        private TabPage BuildRestore()
        {
            var page = new TabPage("Restore");
            var t = Page(page);
            Heading(t, "Timing");
            restoreDelay = Number(0, 60, 1, 0.5m);
            Row(t, "Wait before restoring (seconds):", restoreDelay, tip: "How long ScreenHerder waits after monitors change before moving windows and icons back. 0 lets it decide on its own. If windows land on the wrong screen after you dock, try 3 to 5 seconds.", hint:
                "0 lets ScreenHerder decide. Raise it if a dock or monitor takes a while to wake up and windows land in the wrong place.");
            Heading(t, "Desktop Icons");
            restoreIcons = Check(t, "Put desktop icons back too", tip: "Desktop icons go back to exactly where they were along with your windows: after monitor changes, when you load a layout, and when you undo. Deleted icons leave an empty spot; new icons stay where they are.", hint:
                "Icons return to exactly where they were, both after a monitor change and when you load a layout. Icons you've deleted leave an empty spot; icons added since stay where they are. Turns off the desktop's Auto arrange icons option, which would otherwise pack icons together.");
            Heading(t, "How Windows Are Put Back");
            zorderMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
            zorderMode.Items.AddRange(new object[] { "Never", "Saved layouts only", "Always" });
            Row(t, "Restore which window is on top:", zorderMode, tip: "Windows that overlap also have a front-to-back order. Never leaves the order alone, Saved layouts only restores it when you load a layout, Always restores it after monitor changes too.", hint:
                "Puts overlapping windows back in the same front-to-back order.");
            fastRestore = Check(t, "Fast restore", "Restores in fewer passes. Turn off if some windows end up slightly off.",
                "Moves windows in fewer passes so restores finish sooner. A few apps need a second nudge; if some windows end up a little off, turn this off.");
            fixOffscreen = Check(t, "Bring windows that end up off-screen back onto a monitor", null,
                "If a window's saved spot was on a monitor that isn't connected now, it's moved onto one you can see instead of disappearing.");
            enhancedOffscreen = Check(t, "Use the stronger off-screen check", "Also catches windows that are only partly visible.",
                "Also rescues windows that hang mostly off the edge of a screen, not just ones that are completely out of view.");
            fixTaskbar = Check(t, "Restore the taskbar position too", null,
                "If you keep the taskbar on a particular side or screen, it's put back there after a monitor change.");
            fixUnminimized = Check(t, "Fix windows that were minimized when the monitors changed", null,
                "Windows that were minimized during a monitor change come back to their proper spot and size when you open them again.");
            restoreNewWindows = Check(t, "Open new windows where that app's window was last", "When you reopen an app, its window returns to the spot it last had.",
                "When you close an app and open it again, its window comes back where it was instead of wherever the app picks.");
            restoreClosed = Check(t, "Reopen apps that are part of a layout but were closed", "Off by default. When restoring, ScreenHerder starts any missing app again.",
                "When you restore, any app that was open when the layout was saved but has since been closed is started again and put in its spot.");
            showDesktop = Check(t, "Show the desktop briefly when monitors change", null,
                "Minimizes everything for a moment while monitors switch, then brings it back. Can reduce flicker on some docks.");
            return page;
        }

        private TabPage BuildShortcuts()
        {
            var page = new TabPage("Shortcuts");
            var t = Page(page);
            Heading(t, "System-Wide Shortcuts");
            Hint(t, "Click a box and press the keys you want. Backspace clears it. These work from any app.");
            hkQuick = new HotkeyBox();
            Row(t, "Open the quick menu:", hkQuick, tip: "Opens the same menu as left-clicking the sheep, wherever your mouse is. Click the box and press the keys you want; Backspace clears it.");
            hkSaveAs = new HotkeyBox();
            Row(t, "Save desktop as:", hkSaveAs, tip: "Jumps straight to naming and saving the current arrangement of windows and icons. Click the box and press the keys you want; Backspace clears it.");
            hkUndo = new HotkeyBox();
            Row(t, "Undo last restore:", hkUndo, tip: "Puts windows and icons back the way they were just before you last loaded a layout. Click the box and press the keys you want; Backspace clears it.");
            Heading(t, "Inside The Quick Menu");
            Hint(t, "While the left-click menu is open, press a layout's letter or number to restore it. Hold Shift and press a letter or number to save the current desktop into that slot.");
            return page;
        }

        private TabPage BuildLayouts()
        {
            var page = layoutsPage = new TabPage("Layouts");
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(14) };
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            panel.Controls.Add(new Label
            {
                Text = "Every layout you have saved, on every monitor setup. A layout can only be loaded while the same monitors are connected. Changes here take effect immediately.",
                AutoSize = true,
                MaximumSize = new Size(660, 0),
                Margin = new Padding(0, 0, 0, 8)
            });

            layoutList = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                Dock = DockStyle.Fill
            };
            layoutList.Columns.Add("Name", 170);
            layoutList.Columns.Add("Key", 45);
            layoutList.Columns.Add("Monitors", 230);
            layoutList.Columns.Add("Last Used", 125);
            layoutList.Columns.Add("Status", 110);
            layoutList.SelectedIndexChanged += (o, e) => UpdateLayoutButtons();
            layoutList.DoubleClick += (o, e) => { if (loadBtn.Enabled) LoadSelected(); };
            panel.Controls.Add(layoutList);

            loadBtn = new Button { Text = "Load", AutoSize = true };
            renameBtn = new Button { Text = "Rename…", AutoSize = true };
            deleteBtn = new Button { Text = "Delete", AutoSize = true };
            loadBtn.Click += (o, e) => LoadSelected();
            Tip("Puts the windows and icons back the way they were in the selected layout. Only works with the monitors it was saved on.", loadBtn);
            Tip("Gives the selected layout a new name. Its letter and contents stay the same.", renameBtn);
            Tip("Removes the selected layout for good.", deleteBtn);
            Tip("Every saved layout. Grey ones were saved on different monitors and can only be loaded when those monitors are connected. Double-click a layout to load it.", layoutList);
            renameBtn.Click += (o, e) => RenameSelected();
            deleteBtn.Click += (o, e) => DeleteSelected();
            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0) };
            buttons.Controls.Add(loadBtn);
            buttons.Controls.Add(renameBtn);
            buttons.Controls.Add(deleteBtn);
            panel.Controls.Add(buttons);

            page.Controls.Add(panel);
            return page;
        }

        private TabPage BuildAdvanced()
        {
            var page = new TabPage("Advanced");
            var t = Page(page);
            Heading(t, "Apps To Leave Alone");
            ignoreProcs = new TextBox { Width = 380 };
            Row(t, "Ignore these apps:", ignoreProcs, tip: "Windows from these programs are never moved or recorded. Use the program's file name, like zoom.exe, and separate several with semicolons.", hint:
                "Program names separated by semicolons, for example: zoom.exe;obs64.exe. ScreenHerder never moves their windows.");
            Heading(t, "Tracking");
            captureDelay = Number(0, 30, 1, 0.5m);
            Row(t, "Wait before recording a moved window (seconds):", captureDelay, "0 lets ScreenHerder decide.",
                "After you move or resize a window, ScreenHerder waits this long before remembering the new spot. 0 lets it decide; a longer wait ignores quick temporary moves.");
            Heading(t, "Extra Window Tricks");
            ctrlMinimize = Check(t, "Ctrl + minimize sends a window to the notification area (system tray)", null,
                "Hold Ctrl while clicking any window's minimize button and the window hides as an icon by the clock instead of on the taskbar. Click that icon to bring it back.");
            swapAlt = Check(t, "Alt + click a background window swaps its position with the front window", null,
                "Hold Alt and click a window that's behind another one, and the two trade places and sizes.");
            dualPos = Check(t, "Dual position: a window can keep one size in front and another behind", null,
                "Lets a window remember two positions: a big one while you're working in it and a smaller one when it's in the background.");
            windowCommander = Check(t, "Alt+W browser window commander", null,
                "Press Alt+W over a web browser to open a small command box for that window. Off by default.");
            Heading(t, "Your Data");
            var openData = new Button { Text = "Open Data Folder", AutoSize = true };
            openData.Click += (o, e) => Process.Start("explorer.exe", Program.AppdataFolder);
            Tip("Opens the folder where ScreenHerder keeps your settings, layouts and window history.", openData);
            t.Controls.Add(openData);
            t.SetColumnSpan(openData, 2);
            return page;
        }

        // =================================================================
        // Section 4: move values between the settings object and controls
        // =================================================================
        private void LoadFrom(ShSettings v)
        {
            quickCount.Value = v.QuickMenuCount;
            showSplash.Checked = v.ShowSplash;
            notifyRestore.Checked = v.NotifyOnRestore;
            askBeforeRestore.Checked = v.AskBeforeAutoRestore;
            minimizeToTray.Checked = v.MinimizeToTray;
            hoverTips.Checked = v.ShowHoverTips;

            restoreDelay.Value = (decimal)Math.Min(60, v.RestoreDelaySeconds);
            zorderMode.SelectedIndex = v.ZOrderMode;
            fastRestore.Checked = v.FastRestore;
            fixOffscreen.Checked = v.FixOffscreen;
            enhancedOffscreen.Checked = v.EnhancedOffscreenFix;
            fixTaskbar.Checked = v.FixTaskbar;
            fixUnminimized.Checked = v.FixUnminimized;
            restoreNewWindows.Checked = v.RestoreNewWindowsToLastPosition;
            restoreClosed.Checked = v.RestoreClosedWindows;
            showDesktop.Checked = v.ShowDesktopWhenDisplayChanges;
            restoreIcons.Checked = v.RestoreDesktopIcons;

            hkQuick.Value = v.HotkeyQuickMenu;
            hkSaveAs.Value = v.HotkeySaveAs;
            hkUndo.Value = v.HotkeyUndo;

            ignoreProcs.Text = v.IgnoreProcesses;
            captureDelay.Value = (decimal)Math.Min(30, v.CaptureDelaySeconds);
            ctrlMinimize.Checked = v.CtrlMinimizeToTray;
            swapAlt.Checked = v.SwapOnAltActivate;
            dualPos.Checked = v.DualPosition;
            windowCommander.Checked = v.WindowCommander;
        }

        private ShSettings ReadControls()
        {
            var v = new ShSettings
            {
                QuickMenuCount = (int)quickCount.Value,
                ShowSplash = showSplash.Checked,
                NotifyOnRestore = notifyRestore.Checked,
                AskBeforeAutoRestore = askBeforeRestore.Checked,
                MinimizeToTray = minimizeToTray.Checked,
                ShowHoverTips = hoverTips.Checked,

                RestoreDelaySeconds = (double)restoreDelay.Value,
                ZOrderMode = zorderMode.SelectedIndex,
                FastRestore = fastRestore.Checked,
                FixOffscreen = fixOffscreen.Checked,
                EnhancedOffscreenFix = enhancedOffscreen.Checked,
                FixTaskbar = fixTaskbar.Checked,
                FixUnminimized = fixUnminimized.Checked,
                RestoreNewWindowsToLastPosition = restoreNewWindows.Checked,
                RestoreClosedWindows = restoreClosed.Checked,
                ShowDesktopWhenDisplayChanges = showDesktop.Checked,
                RestoreDesktopIcons = restoreIcons.Checked,

                HotkeyQuickMenu = hkQuick.Value,
                HotkeySaveAs = hkSaveAs.Value,
                HotkeyUndo = hkUndo.Value,

                IgnoreProcesses = ignoreProcs.Text.Trim(),
                CaptureDelaySeconds = (double)captureDelay.Value,
                CtrlMinimizeToTray = ctrlMinimize.Checked,
                SwapOnAltActivate = swapAlt.Checked,
                DualPosition = dualPos.Checked,
                WindowCommander = windowCommander.Checked
            };
            return v;
        }

        // =================================================================
        // Section 5: save. Menu count and shortcuts apply right away;
        // anything that changes how the engine runs needs a restart.
        // =================================================================
        private void SaveAndClose()
        {
            var v = ReadControls();

            // the three shortcuts must differ from each other
            // (desktop icon tracking applies immediately; no restart needed)
            var hk = new[] { v.HotkeyQuickMenu, v.HotkeySaveAs, v.HotkeyUndo };
            for (int i = 0; i < hk.Length; i++)
                for (int j = i + 1; j < hk.Length; j++)
                    if (hk[i] != 0 && hk[i] == hk[j])
                    {
                        MessageBox.Show(this, "Two shortcuts use the same keys. Give each action its own shortcut.",
                            "ScreenHerder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

            if (autoStart.Enabled && autoStart.Checked != autoStartWas)
            {
                if (!AutoStart.Set(autoStart.Checked))
                {
                    MessageBox.Show(this, "Windows didn't allow the change to starting at sign-in, so it was left as it was. Your other settings will still be saved.",
                        "ScreenHerder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    autoStart.Checked = autoStartWas;
                }
                else
                    autoStartWas = autoStart.Checked;
            }

            bool restart = Program.EngineArgs(v) != Program.EngineArgs(s);
            Program.ApplySettings(v);

            if (restart)
            {
                Close();
                Program.RestartForSettings();
                return;
            }
            Close();
        }

        // jump straight to the Layouts tab (from "All Layouts…")
        public void ShowLayoutsTab()
        {
            tabs.SelectedTab = layoutsPage;
            RefreshLayouts();
        }

        // =================================================================
        // Section 6: Layouts tab actions
        // =================================================================
        private void RefreshLayouts()
        {
            string current = Program.pwp.CurrentDisplayKey;
            layoutList.BeginUpdate();
            layoutList.Items.Clear();
            foreach (var l in Program.Layouts.All())
            {
                bool here = l.DisplayKey == current;
                bool exists = Program.pwp.HasSnapshot(l.DisplayKey, l.Slot);
                string status = !exists ? "Not available" : here ? "This setup" : "Other setup";
                var item = new ListViewItem(new[]
                {
                    l.Name,
                    char.ToUpperInvariant(l.SlotChar).ToString(),
                    LayoutStore.DescribeMonitors(l.DisplayKey),
                    l.LastUsed.ToString("g", CultureInfo.CurrentCulture),
                    status
                });
                item.Tag = l;
                if (!here || !exists)
                    item.ForeColor = SystemColors.GrayText;
                layoutList.Items.Add(item);
            }
            layoutList.EndUpdate();
            UpdateLayoutButtons();
        }

        private NamedLayout Selected()
        {
            return layoutList.SelectedItems.Count == 1 ? (NamedLayout)layoutList.SelectedItems[0].Tag : null;
        }

        private void UpdateLayoutButtons()
        {
            var l = Selected();
            loadBtn.Enabled = l != null && l.DisplayKey == Program.pwp.CurrentDisplayKey && Program.pwp.HasSnapshot(l.DisplayKey, l.Slot);
            renameBtn.Enabled = l != null;
            deleteBtn.Enabled = l != null;
        }

        private void LoadSelected()
        {
            var l = Selected();
            if (l == null)
                return;
            Program.systrayForm.RestoreLayout(l);
            RefreshLayouts();
        }

        private void RenameSelected()
        {
            var l = Selected();
            if (l == null)
                return;
            using (var dlg = new NameLayoutDialog("Rename Layout", "New name for this layout:", l.Name, LayoutStore.DescribeMonitors(l.DisplayKey)))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                if (!Program.Layouts.Rename(l, dlg.LayoutName))
                    MessageBox.Show(this, "Another layout for those monitors already has that name.", "ScreenHerder",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            RefreshLayouts();
        }

        private void DeleteSelected()
        {
            var l = Selected();
            if (l == null)
                return;
            if (MessageBox.Show(this, "Delete the layout \"" + l.Name + "\"? This can't be undone.", "ScreenHerder",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
                return;
            Program.Layouts.Remove(l);
            Program.pwp.DeleteSnapshot(l.DisplayKey, l.Slot);
            Program.systrayForm.Icons.ForgetLayout(l.DisplayKey, l.Slot);
            RefreshLayouts();
        }
    }
}
