using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BloodDonationCoin
{
    public partial class Splash : Form
    {
        public Splash()
        {
            InitializeComponent();
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }

        private void Splash_Load(object sender, EventArgs e)
        {
            this.Show();
            this.Refresh();


            System.Diagnostics.Process[] p = System.Diagnostics.Process.GetProcesses();
            foreach (var item in p)
            {
                if (item.ProcessName.ToLower().Trim() == "bbrd")
                    item.Kill();
                else if (item.ProcessName.ToLower().Trim() == "simplewallet")
                    item.Kill();
            }


            System.Threading.Thread.Sleep(5000);
            WalletPicker a = new WalletPicker();
            this.Hide();
            a.ShowDialog();            
            Application.Exit();
            
        }
    }
}
