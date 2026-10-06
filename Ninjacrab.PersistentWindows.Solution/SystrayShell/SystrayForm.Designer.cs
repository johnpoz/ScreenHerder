using System.Windows.Forms;

namespace PersistentWindows.SystrayShell
{
    partial class SystrayForm
    {
        // =================================================================
        // Tray icon and its two menus:
        //   quickMenu               left-click: layouts and layout actions
        //   contextMenuStripSysTray right-click: Settings, Help, Exit
        // =================================================================
        private System.ComponentModel.IContainer components = null;
        public NotifyIcon notifyIconMain;
        public ContextMenuStrip contextMenuStripSysTray;
        public QuickMenuStrip quickMenu;

        private ToolStripMenuItem settingsMenuItem;
        private ToolStripMenuItem helpMenuItem;
        private ToolStripMenuItem exitMenuItem;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.notifyIconMain = new NotifyIcon(this.components);
            this.contextMenuStripSysTray = new ContextMenuStrip(this.components);
            this.quickMenu = new QuickMenuStrip(this.components);
            this.settingsMenuItem = new ToolStripMenuItem();
            this.helpMenuItem = new ToolStripMenuItem();
            this.exitMenuItem = new ToolStripMenuItem();
            this.contextMenuStripSysTray.SuspendLayout();
            this.SuspendLayout();

            // ---------------- tray icon ----------------
            this.notifyIconMain.ContextMenuStrip = this.contextMenuStripSysTray;
            this.notifyIconMain.Icon = Program.IdleIcon;
            this.notifyIconMain.Text = "ScreenHerder";
            this.notifyIconMain.BalloonTipTitle = "";
            this.notifyIconMain.BalloonTipText = "Restoring your windows";
            this.notifyIconMain.BalloonTipIcon = ToolTipIcon.Info;
            this.notifyIconMain.Visible = Program.Gui;
            this.notifyIconMain.MouseUp += new MouseEventHandler(this.IconMouseUp);

            // ---------------- right-click menu ----------------
            this.settingsMenuItem.Text = "Settings…";
            this.settingsMenuItem.Font = new System.Drawing.Font(this.settingsMenuItem.Font, System.Drawing.FontStyle.Bold);
            this.settingsMenuItem.Click += (s, e) => OpenSettings();
            this.settingsMenuItem.ToolTipText = "Change how ScreenHerder restores, its shortcuts, and manage saved layouts.";
            this.helpMenuItem.Text = "Help";
            this.helpMenuItem.Click += (s, e) => OpenHelp();
            this.helpMenuItem.ToolTipText = "How ScreenHerder works, version, and credits.";
            this.exitMenuItem.Text = "Exit";
            this.exitMenuItem.Click += (s, e) => Exit();
            this.exitMenuItem.ToolTipText = "Stop ScreenHerder until you start it again or next sign in.";
            this.contextMenuStripSysTray.ShowImageMargin = false;
            this.contextMenuStripSysTray.Items.AddRange(new ToolStripItem[] {
                this.settingsMenuItem,
                this.helpMenuItem,
                new ToolStripSeparator(),
                this.exitMenuItem });
            this.contextMenuStripSysTray.Name = "contextMenuStripSysTray";

            // ---------------- left-click quick menu ----------------
            this.quickMenu.Name = "quickMenu";

            // ---------------- hidden host form ----------------
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(120, 0);
            this.ShowInTaskbar = false;
            this.WindowState = FormWindowState.Minimized;
            this.Name = "SystrayForm";
            this.Text = "ScreenHerder";
            this.contextMenuStripSysTray.ResumeLayout(false);
            this.ResumeLayout(false);
        }
    }
}
