using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PersistentWindows.SystrayShell
{
    // =====================================================================
    // Help / About window: how to use ScreenHerder, version, credits
    // =====================================================================
    public class HelpForm : Form
    {
        public HelpForm()
        {
            // ---------------- Section 1: window chrome ----------------
            Text = "ScreenHerder Help";
            Icon = Program.IdleIcon;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            Font = SystemFonts.MessageBoxFont;
            ClientSize = new Size(620, 520);
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;

            // ---------------- Section 2: header ----------------
            var logo = new PictureBox
            {
                Image = Properties.Resources.pwIcon,
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(56, 56),
                Location = new Point(16, 16)
            };
            var title = new Label
            {
                Text = "ScreenHerder " + Application.ProductVersion,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 14F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(84, 20)
            };
            var tagline = new Label
            {
                Text = "Puts your windows back when your monitors change.",
                AutoSize = true,
                Location = new Point(86, 50),
                ForeColor = SystemColors.GrayText
            };

            // ---------------- Section 3: how-to text ----------------
            var body = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = SystemColors.Control,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(18, 88),
                Size = new Size(584, 360),
                TabStop = false,
                Text = string.Join(Environment.NewLine, new[]
                {
                    "AUTOMATIC",
                    "ScreenHerder watches your windows and desktop icons. When you plug in or unplug monitors, it puts every window and every desktop icon back where it was the last time you used that exact set of monitors. There is nothing to do.",
                    "Icons you've deleted since leave an empty spot; icons you've added stay where they are.",
                    "",
                    "LEFT-CLICK THE TRAY ICON",
                    "Opens the quick menu:",
                    "  - Your most recent saved layouts for the monitors connected right now. Click one to restore it.",
                    "  - Save Desktop As: name and save the current arrangement of windows and desktop icons.",
                    "  - Undo Last Restore: go back to how things were before the last layout you loaded.",
                    "  - All Layouts: see, load, rename or delete every saved layout.",
                    "While the menu is open, press a layout's letter to restore it, or Shift + a letter to save into that slot.",
                    "Loading a layout also reopens apps from it that aren't running and moves them into place (Store apps included). Uninstalled apps are skipped.",
                    "",
                    "RIGHT-CLICK THE TRAY ICON",
                    "Settings, Help and Exit. In Settings, rest the pointer on any option for 3 seconds to see what it does (turn tips off under General).",
                    "",
                    "STARTING AND MINIMIZING",
                    "ScreenHerder starts when you sign in; turn that off under Settings > General. Minimized ScreenHerder windows hide in the tray; right-click the sheep and choose Settings or Help to bring them back.",
                    "",
                    "SHORTCUTS",
                    "Ctrl+Alt+L opens the quick menu from anywhere. Change it, or add shortcuts for Save Desktop As and Undo, under Settings > Shortcuts.",
                    "",
                    "LAYOUTS AND MONITORS",
                    "Each layout belongs to the set of monitors it was saved on. The quick menu only shows layouts you can use with the monitors you have now; Settings > Layouts shows all of them.",
                    "",
                    "CREDITS AND LICENSE",
                    "ScreenHerder includes the window-tracking engine of PersistentWindows by Kang Yu and contributors, and adds desktop icon restore, named layouts, and its own menus and settings. It is distributed under the GNU General Public License v3.0.",
                })
            };

            var upstream = new LinkLabel
            {
                Text = "PersistentWindows project",
                AutoSize = true,
                Location = new Point(18, 470)
            };
            upstream.LinkClicked += (o, e) => Process.Start("https://github.com/kangyu-california/PersistentWindows");
            var license = new LinkLabel
            {
                Text = "GPL-3.0 license",
                AutoSize = true,
                Location = new Point(200, 470)
            };
            license.LinkClicked += (o, e) => Process.Start("https://www.gnu.org/licenses/gpl-3.0.html");

            var close = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.OK, Location = new Point(520, 464) };
            AcceptButton = close;
            CancelButton = close;

            Controls.AddRange(new Control[] { logo, title, tagline, body, upstream, license, close });
            Shown += (o, e) => { Activate(); close.Focus(); };
        }
    }
}
