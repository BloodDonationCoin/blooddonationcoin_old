﻿using System;
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
    public partial class Progress : Form
    {
        public Progress()
        {
            InitializeComponent();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            CenterToParent();
        }

        public void SetProgress(string status, int percentage)
        {
            lblStatus.Text = status;
            progressBar.Value = percentage;
        }

        private void lblStatus_Click(object sender, EventArgs e)
        {

        }
    }
}
