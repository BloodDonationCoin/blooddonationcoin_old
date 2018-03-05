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
    public partial class LoginPrompt : Form
    {
        public bool ShouldLogin { get; set; }
        public string Password { get; set; }

        public LoginPrompt()
        {
            InitializeComponent();

            tbPassword.Focus();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            
            CenterToParent();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;

            Close();
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            if (tbPassword.Text.Trim().Length < 6)
            {
                MessageBox.Show("Min 6 length password!");
                return;
            }
            Login();
        }

        private void tbPassword_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                Login();
            }
        }

        private void Login()
        {
            DialogResult = DialogResult.OK;
            Password = tbPassword.Text;

            Close();
        }

        private void LoginPrompt_Load(object sender, EventArgs e)
        {

        }
    }
}
