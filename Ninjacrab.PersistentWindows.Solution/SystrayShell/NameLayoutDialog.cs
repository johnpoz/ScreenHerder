using System;
using System.Drawing;
using System.Windows.Forms;

namespace PersistentWindows.SystrayShell
{
    // =====================================================================
    // "Save Desktop As" / "Rename Layout" prompt.
    // Built in code (no designer file) so it stays easy to read and edit.
    // =====================================================================
    public class NameLayoutDialog : Form
    {
        private readonly TextBox nameBox = new TextBox();
        private readonly Button okButton = new Button();
        private readonly Button cancelButton = new Button();

        public string LayoutName
        {
            get { return nameBox.Text.Trim(); }
        }

        public NameLayoutDialog(string title, string prompt, string initialName, string monitorsText)
        {
            // ---------------- Section 1: window chrome ----------------
            Text = title;
            Icon = Program.IdleIcon;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            Font = SystemFonts.MessageBoxFont;
            Padding = new Padding(12);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            // ---------------- Section 2: controls ----------------
            var layout = new TableLayoutPanel
            {
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill
            };

            var promptLabel = new Label { Text = prompt, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
            var monitorsLabel = new Label
            {
                Text = monitorsText,
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 0, 0, 8)
            };

            nameBox.Width = 340;
            nameBox.MaxLength = 60;
            nameBox.Text = initialName ?? "";
            nameBox.Margin = new Padding(0, 0, 0, 12);
            nameBox.TextChanged += (s, e) => okButton.Enabled = LayoutName.Length > 0;

            okButton.Text = "Save";
            okButton.DialogResult = DialogResult.OK;
            okButton.AutoSize = true;
            okButton.Enabled = LayoutName.Length > 0;
            cancelButton.Text = "Cancel";
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.AutoSize = true;

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };
            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(okButton);

            layout.Controls.Add(promptLabel);
            if (!string.IsNullOrEmpty(monitorsText))
                layout.Controls.Add(monitorsLabel);
            layout.Controls.Add(nameBox);
            layout.Controls.Add(buttons);
            Controls.Add(layout);

            AcceptButton = okButton;
            CancelButton = cancelButton;

            // ---------------- Section 3: focus the name, text selected ----------------
            Shown += (s, e) =>
            {
                Activate();
                nameBox.Focus();
                nameBox.SelectAll();
            };
        }
    }
}
