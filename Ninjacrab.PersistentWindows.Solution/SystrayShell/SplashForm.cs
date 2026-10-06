using System;
using PersistentWindows.Common;
using System.Windows.Forms;

namespace PersistentWindows.SystrayShell
{
    public partial class SplashForm : Form
    {
        public SplashForm()
        {
            InitializeComponent();
        }

        private void label1_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(Program.ProjectUrl);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            progressBar1.PerformStep();
            if (progressBar1.Value == progressBar1.Maximum)
            {
                this.Close();
            }
        }

        private void SplashForm_Load(object sender, EventArgs e)
        {
            // ScreenHerder branding; second line credits the upstream engine
            this.label1.Text = "ScreenHerder " + Application.ProductVersion + " is running in the notification area.";
            this.label2.Text = "Built on PersistentWindows";
        }

        private void label2_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(Program.Contributors);
        }
    }
}
