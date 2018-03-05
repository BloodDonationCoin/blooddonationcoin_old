using BloodDonationCoin.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BloodDonationCoin
{
    public partial class MainWindow : Form
    {
        private delegate void SetTextDelegate(string text);

        private DaemonWrapper Daemon { get; set; }
        private WalletWrapper Wallet { get; set; }
        private MinerWrapper MinerManager { get; set; }

        private BindingList<LogLine> DaemonLogLines { get; set; }
        private BindingList<LogLine> WalletLogLines { get; set; }
        private BindingList<Transaction> Transactions { get; set; }

        private bool skipExitWrappers;
        private bool WalletHasBeenReady { get; set; }

        public MainWindow(string path, bool isNew,string password)
        {
            InitializeComponent();

            Transactions = new BindingList<Transaction>();
            dgTransactions.DataSource = Transactions;

            int refreshInterval = int.Parse(ConfigurationManager.AppSettings["WalletRefreshInterval"]);
            int pingInterval = int.Parse(ConfigurationManager.AppSettings["DaemonPingInterval"]);
            int connectionCountInterval = int.Parse(ConfigurationManager.AppSettings["DaemonConnectionCountInterval"]);

            string daemonClientExe = ConfigurationManager.AppSettings["DaemonFileName"];
            string walletClientExe = ConfigurationManager.AppSettings["WalletClientFileName"];
            string minerExe = ConfigurationManager.AppSettings["MinerFileName"];

            Daemon = new DaemonWrapper(path, daemonClientExe, pingInterval, connectionCountInterval);
            Wallet = new WalletWrapper(path, walletClientExe, isNew, refreshInterval,password);
            MinerManager = new MinerWrapper(path, minerExe);

            Text = string.Format(
                "{0} v{1}.{2}.{3}.{4}",
                Text,
                Assembly.GetEntryAssembly().GetName().Version.Major,
                Assembly.GetEntryAssembly().GetName().Version.Minor,
                Assembly.GetEntryAssembly().GetName().Version.Build,
                Assembly.GetEntryAssembly().GetName().Version.Revision);

            //tbPoolMinerThreads.Value = Environment.ProcessorCount;
            
            FillMiningPools();

            // Initialize and start daemon.
            DaemonLogLines = new BindingList<LogLine>();
            Daemon.OutputReceived += new EventHandler<WrapperEvent<LogLine>>((s, e) => DispatchEvent(() => AddLogText(e.Data, DaemonLogLines)));
            Daemon.StatusChanged += new EventHandler<WrapperStatusEvent>((s, e) => DispatchEvent(() => SetStatus(e.Status, e.Message)));
            Daemon.ConnectionsCounted += new EventHandler<WrapperEvent<int>>((s, e) => DispatchEvent(() => SetConnectionCount(e.Data)));
            Daemon.UpdateSoloMiningHashRate += new EventHandler<WrapperEvent<decimal>>((s, e) => DispatchEvent(() => UpdateSoloMiningHashRate(e.Data)));
            Daemon.Start();

            // Initialize and start wallet client.
            WalletLogLines = new BindingList<LogLine>();
            Wallet.ReadyToLogin += new EventHandler<EventArgs>((s, e) => DispatchEvent(() => ShowLogin()));
            Wallet.StatusChanged += new EventHandler<WrapperStatusEvent>((s, e) => DispatchEvent(() => SetStatus(e.Status, e.Message)));
            Wallet.AddressReceived += new EventHandler<WrapperEvent<string>>((s, e) => DispatchEvent(() => SetAddress(e.Data)));
            Wallet.OutputReceived += new EventHandler<WrapperEvent<LogLine>>((s, e) => DispatchEvent(() => AddLogText(e.Data, WalletLogLines)));
            Wallet.BalanceUpdated += new EventHandler<WrapperBalanceEvent>((s, e) => DispatchEvent(() => SetBalance(e.Total, e.Unlocked)));
            Wallet.Error += new EventHandler<WrapperErrorEvent>((s, e) => DispatchEvent(() => ShowError(e.Message, e.ShouldExit)));
            Wallet.Information += new EventHandler<WrapperEvent<string>>((s, e) => DispatchEvent(() => ShowInformation(e.Data)));
            Wallet.TransactionsFetched += new EventHandler<WrapperEvent<IList<Transaction>>>((s, e) => DispatchEvent(() => RefreshTransactions(e.Data)));
            Wallet.WalletReadyToSpent += new EventHandler<WrapperEvent<bool>>((s, e) => DispatchEvent(() => WalletReadyToSpent(e.Data)));
            Wallet.Start();
        }

        /// <summary>
        /// Populate dropdown with mining pools.
        /// </summary>
        private void FillMiningPools()
        {
            //var pools = ConfigurationManager.AppSettings["MiningPoolAddresses"]
            //    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            //lbMiningPools.DataSource = pools;

            //if (lbMiningPools.Items.Count > 0)
            //{
            //    lbMiningPools.SelectedIndex = 0;
            //}
        }

        /// <summary>
        /// Safely dispatch an event to an action handler.
        /// </summary>
        /// <param name="handler"></param>
        private void DispatchEvent(Action handler)
        {
            if (!Disposing && !IsDisposed && InvokeRequired)
            {
                Invoke(handler);
            }
            else
            {
                handler();
            }
        }

        /// <summary>
        /// Show general error message.
        /// </summary>
        /// <param name="message"></param>
        private void ShowError(string message, bool shouldExit)
        {
            MessageBox.Show(this, message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);

            if (shouldExit)
            {
                Close();
            }
        }

        /// <summary>
        /// Show general information message.
        /// </summary>
        /// <param name="message"></param>
        private void ShowInformation(string message)
        {
            MessageBox.Show(this, message, "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Transfer coins.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SendClick(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(tbSendAddress.Text)
                || tbSendAmount.Value == 0M)
            {
                MessageBox.Show("You need to enter an address, an amount and a mixin count.", "Input error", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                btnSend.Enabled = false;

                string address = tbSendAddress.Text;
                decimal amount = tbSendAmount.Value;
                int mixin = (int)tbSendMixin.Value;                

                Wallet.Transfer(address, amount, mixin, "");
            }
        }

        /// <summary>
        /// Show login window. Login to wallet when login is entered. If users cancels, close application.
        /// </summary>
        private void ShowLogin()
        {
            var loginPrompt = new LoginPrompt();
            if (loginPrompt.ShowDialog(this) == DialogResult.OK)
            {
                Wallet.Login(loginPrompt.Password);
            }
            else
            {
                Close();
            }
        }

        /// <summary>
        /// Sets the balance.
        /// </summary>
        /// <param name="total"></param>
        /// <param name="unlocked"></param>
        private void SetBalance(decimal total, decimal unlocked)
        {
            lblBalance.Text = string.Format("{0} BBRC", unlocked);
            lblUnconfirmed.Text = string.Format("{0} BBRC", total);
        }

        /// <summary>
        /// Refresh the datasource of transactions.
        /// </summary>
        /// <param name="transactions"></param>
        private void RefreshTransactions(IList<Transaction> transactions)
        {
            foreach (var newTransaction in transactions
                .Where(tr => !Transactions.Any(knownTransaction => knownTransaction.TransactionId == tr.TransactionId)))
            {
                Transactions.Add(newTransaction);
            }
        }

        /// <summary>
        /// Enables or disables the sent button.
        /// </summary>
        /// <param name="readyToSpent"></param>
        private void WalletReadyToSpent(bool readyToSpent)
        {
            btnSend.Enabled = readyToSpent;
        }

        /// <summary>
        /// Set status in status bar.
        /// </summary>
        /// <param name="status"></param>
        /// <param name="message"></param>
        private void SetStatus(WalletStatus status, string message)
        {
            WalletHasBeenReady = WalletHasBeenReady || status == WalletStatus.Ready;

            lblStatus.Text = message;

            // Once the daemon has synced, ignore any synchronisation events.
            if (WalletHasBeenReady
                && (status == WalletStatus.SynchronizingBlockchain || status == WalletStatus.SynchronizingWallet))
            {
                return;
            }

            btnSend.Enabled = status == WalletStatus.Ready;
            

            if (status == WalletStatus.Ready && !Wallet.RefreshTimer.Enabled)
            {
                Wallet.RefreshTimer.Start();
            }
        }

        /// <summary>
        /// Set the connection count in status bar.
        /// </summary>
        /// <param name="connectionCount"></param>
        private void SetConnectionCount(int connectionCount)
        {
            lblConnections.Text = string.Format("{0} peers", connectionCount);
            //lblTotalBlock.Text = Wallet.BlockHeight.ToString();
        }

        /// <summary>
        /// Update current wallet address.
        /// </summary>
        /// <param name="address"></param>
        private void SetAddress(string address)
        {
            lblAddress.Text = address;
            linkLabel1.Enabled = true;

            //tbPoolLogin.Text = address;
        }

        /// <summary>
        /// Update wallet log.
        /// </summary>
        /// <param name="logLine"></param>
        /// <param name="logLines"></param>
        private void AddLogText(LogLine logLine, IList<LogLine> logLines)
        {
            while (logLines.Count >= 1000)
            {
                logLines.RemoveAt(0);
            }

            logLines.Add(logLine);
        }

        /// <summary>
        /// When the form closes, make sure the wallet stops refreshing and all work is saved.
        /// </summary>
        /// <param name="e"></param>
        protected async override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            System.Diagnostics.Process[] p = System.Diagnostics.Process.GetProcesses();
            foreach (var item in p)
            {
                if (item.ProcessName.ToLower().Trim() == "bbrd")
                    item.Kill();
                else if (item.ProcessName.ToLower().Trim() == "simplewallet")
                    item.Kill();
            }
            Close();
            return;
            if (!skipExitWrappers)
            {
                Cursor = Cursors.WaitCursor;
                SetStatus(WalletStatus.Ready, "Exiting...");

                skipExitWrappers = true;
                e.Cancel = true;

                Progress progess = new Progress();
                progess.Show(this);

                var tf = new TaskFactory();

                progess.SetProgress("Stopping miners", 0);
                //await tf.StartNew(() => MinerManager.Exit());


                progess.SetProgress("Stopping wallet", 33);
                //await tf.StartNew(() => Wallet.Exit());


                progess.SetProgress("Stopping daemon", 66);
                //await tf.StartNew(() => Daemon.Exit());

                Close();
            }
        }

        private void btnCopyAddressClick(object sender, EventArgs e)
        {
        
        }

        /// <summary>
        /// Start solo mining.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnStartSoloMiningClick(object sender, EventArgs e)
        {
           
        }

        /// <summary>
        /// Update solo mining hash rate display on solo mining button.
        /// </summary>
        /// <param name="hr"></param>
        private void UpdateSoloMiningHashRate(decimal hr)
        {
            
        }

        /// <summary>
        /// Start pool miners.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnStartPoolMiningClick(object sender, EventArgs e)
        {
            //lbMiningPools.Enabled = MinerManager.IsMinig;
            //tbPoolLogin.Enabled = MinerManager.IsMinig;
            //tbPoolPassword.Enabled = MinerManager.IsMinig;
            //tbPoolMinerThreads.Enabled = MinerManager.IsMinig;
            //chkShowPoolMinerWindows.Enabled = MinerManager.IsMinig;

            //if (MinerManager.IsMinig)
            //{
            //    // Stop
            //    MinerManager.Exit();

            //    btnStartPoolMining.Text = "Start mining";
            //}
            //else
            //{
            //    // Start            
            //    if (!string.IsNullOrEmpty(lbMiningPools.Text)
            //        && !string.IsNullOrEmpty(tbPoolLogin.Text)
            //        && !string.IsNullOrEmpty(tbPoolPassword.Text)
            //        && tbPoolMinerThreads.Value != 0M)
            //    {
            //        MinerManager.Start(
            //            lbMiningPools.Text,
            //            tbPoolLogin.Text,
            //            tbPoolPassword.Text,
            //            (int)tbPoolMinerThreads.Value,
            //            chkShowPoolMinerWindows.Checked);

            //        btnStartPoolMining.Text = string.Format("Stop miners ({0})", MinerManager.Processes.Count);
            //    }
            //}
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            

            Close();
            
        }

        private void showWalletLogToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var logWindow = new LogWindow(WalletLogLines, Wallet))
            {
                logWindow.ShowDialog(this);
            }
        }

        private void showDaemonLogToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var logWindow = new LogWindow(DaemonLogLines, Daemon))
            {
                logWindow.ShowDialog(this);
            }
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var aboutWindow = new About(Wallet.WalletVersion, Wallet.BlockHeight))
            {
                aboutWindow.ShowDialog(this);
            }
        }

        private void MainWindow_Load(object sender, EventArgs e)
        {
            
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            
        }

        private void linkLabel1_LinkClicked_1(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Clipboard.SetText(lblAddress.Text);
        }
    }
}
