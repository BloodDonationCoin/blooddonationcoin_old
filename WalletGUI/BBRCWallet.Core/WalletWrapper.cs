using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;

namespace BloodDonationCoin.Core
{
    /// <summary>
    /// Wraps the simplewallet command line application. Sends command and interprets output.
    /// </summary>
    public class WalletWrapper : BaseWrapper
    {
        private List<Transaction> Transactions { get; set; }
        private bool IsNew { get; set; }

        public Timer RefreshTimer { get; set; }

        public string WalletVersion { get; set; }
        public string Password { get; set; }
        public long BlockHeight { get; set; }

        public EventHandler<EventArgs> ReadyToLogin;
        public EventHandler<WrapperEvent<string>> AddressReceived;
        public EventHandler<WrapperBalanceEvent> BalanceUpdated;
        public EventHandler<WrapperEvent<IList<Transaction>>> TransactionsFetched;
        public EventHandler<WrapperEvent<bool>> WalletReadyToSpent;

        public WalletWrapper(string walletPath, string exeFileName, bool isNew, int refreshInterval,string password)
            : base(walletPath, exeFileName)
        {
            Transactions = new List<Transaction>();
            IsNew = isNew;
            WalletVersion = "unknown";

            this.Password = password;

            RefreshTimer = new Timer(refreshInterval);
            RefreshTimer.Elapsed += (s, e) => Refresh();
        }

        /// <summary>
        /// Start wallet process and parse output.
        /// </summary>
        public async void Start()
        {
            if (!CanStart())
            {
                return;
            }

            Backup();

            HandleLines = true; 

            WrapperProcess = new Process();

            var processStartInfo = new ProcessStartInfo(ExecutablePath);
            processStartInfo.UseShellExecute = false;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.RedirectStandardError = true;
            processStartInfo.CreateNoWindow = true;

            string pass = "";
            if (Password.Trim() != "")
                pass = " --password " + this.Password;

            if (IsNew)
            {
                
                processStartInfo.Arguments = string.Format("--config-file configs/BloodDonationCoin.conf "+pass+" --generate-new-wallet=\"{0}\"", WalletPath);
            }
            else
            {
                processStartInfo.Arguments = string.Format("--config-file configs/BloodDonationCoin.conf "+pass+" --wallet-file=\"{0}\"", WalletPath);
            }

            WrapperProcess.StartInfo = processStartInfo;
            WrapperProcess.Start();

            TaskFactory factory = new TaskFactory();
            await factory.StartNew(() => ReadNextLine(false));
            await factory.StartNew(() => ReadNextLine(true));
        }

        public override void Exit()
        {
            HandleLines = false;

            RefreshTimer.Stop();

            base.Exit();
        }

        /// <summary>
        /// Create a backup of selected wallet.
        /// </summary>
        public void Backup()
        {
            string walletName = Path.GetFileNameWithoutExtension(WalletPath);
            string walletDir = Path.GetDirectoryName(WalletPath);
            string backupDir = Path.Combine(walletDir, walletName + "_" + DateTime.Now.ToString("yyyyMMdd")); 

            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            foreach (var file in Directory.GetFiles(walletDir, walletName + "*"))
            {
                File.Copy(file, Path.Combine(backupDir, Path.GetFileName(file)), true);
            }
        }

        /// <summary>
        /// Login with given password.
        /// </summary>
        /// <param name="password"></param>
        public void Login(string password)
        {
            WriteLine(password);
        }

        /// <summary>
        /// Transfer amount coins to address using given amount of mixin to hide transaction.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="amount"></param>
        /// <param name="mixin"></param>
        /// <param name="paymentId"></param>
        public void Transfer(string address, decimal amount, int mixin, string paymentId)
        {
            if (string.IsNullOrWhiteSpace(paymentId))
            {
                WriteLine(string.Format(CultureInfo.InvariantCulture, "transfer {0} {1} {2}", mixin, address.Trim(), amount));
            }
            else
            {
                WriteLine(string.Format(CultureInfo.InvariantCulture, "transfer {0} {1} {2} {3}", mixin, address.Trim(), amount, paymentId));
            }            
        }

        /// <summary>
        /// Refresh blocks from daemon.
        /// </summary>
        public void Refresh()
        {                        
            WriteLine("list_transfers");
            GetBalance();
        }

        /// <summary>
        /// Get current balance.
        /// </summary>
        public void GetBalance()
        {
            WriteLine("balance");
        }

        /// <summary>
        /// Interpret the current wallet output and call relevant event listeners.
        /// </summary>
        /// <param name="line">Current line.</param>
        /// <param name="isError">Is the line read from StandardError?</param>
        /// <param name="lineIsHandled">Has the line been handled?</param>
        
        private static string beforeLine = "";
        protected override void HandleLine(string line, bool isError, bool lineIsHandled = true)
        {
            
            bool isFetchingTransactions = false;

            try
            {
                line = System.Text.RegularExpressions.Regex.Split(line, "INFO")[1].Trim();                
            }
            catch
            { }

            if (line.Contains("Opened wallet: "))
            {
                String address = System.Text.RegularExpressions.Regex.Split(line, "Opened wallet:")[1].Trim();                                        
                address = address.Trim();

                if (AddressReceived != null)
                {
                    AddressReceived.Invoke(this, new WrapperEvent<string>(address));
                }
            }
            else if (line.Contains("Generated new wallet: "))
            {
                string address = line.Substring("Generated new wallet: ".Length - 1);
                address = address.Trim();

                if (AddressReceived != null)
                {
                    AddressReceived.Invoke(this, new WrapperEvent<string>(address));
                }
            }
            else if (line.Contains("bitmonero wallet"))
            {
                Match match = Regex.Match(line, "bitmonero wallet v([0-9\\.\\(\\)]+)");
                if (match.Success)
                {
                    WalletVersion = match.Groups[1].Value;
                }

                if (ReadyToLogin != null)
                {
                    ReadyToLogin.Invoke(this, null);
                }
            }
            else if (line.Contains("Error: wrong address"))
            {
                SendError("Invalid send address.", false);

                SetWalletReadyToSpent(true);
            }
            else if (line.Contains("Error: wallet failed to connect"))
            {
                UpdateStatus(WalletStatus.Error, "Can not connect to daemon");
            }
            else if (Regex.IsMatch(line, "Height [0-9]+ of [0-9]+"))
            {
                //Match match = Regex.Match(line, "Height ([0-9]+) of ([0-9]+)");
                //if (match.Success)
                //{
                //    long blockHeight = 0;
                //    if (long.TryParse(match.Groups[2].Value, out blockHeight))
                //    {
                //        BlockHeight = blockHeight;
                //    }

                //    UpdateStatus(
                //        WalletStatus.SynchronizingWallet, 
                //        string.Format("Updating wallet (block {0} of {1})", match.Groups[1], match.Groups[2]));
                //}
            }
            else if (line.Contains("Refresh done"))
            {
                UpdateStatus(WalletStatus.Ready, "Ready");
            }
            else if (line.Contains("Error: failed to load wallet: invalid password"))
            {
                SendError("Invalid password", true);
            }
            else if (line.Contains("Error: payment id has invalid format"))
            {
                SendError("The payment id has an incorrect format. It needs to be a 64 character string.", false);

                SetWalletReadyToSpent(true);
            }
            else if (line.Contains("Error: not enough money"))
            {
                SendError(line, false);

                SetWalletReadyToSpent(true);
            }
            else if (line.Contains("Money successfully sent"))
            {
                String value = System.Text.RegularExpressions.Regex.Split(line, "Money successfully sent, transaction")[1];                                
                SendInformation("Money successfully sent, transaction: " + value);                

                SetWalletReadyToSpent(true);
            }
            else if (line.Contains("balance"))
            {
                Match match = Regex.Match(line, "available balance: ([0-9\\.,]*), locked amount: ([0-9\\.,]*)");
                if (match.Success && BalanceUpdated != null)
                {
                    decimal total = decimal.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                    decimal unlocked = decimal.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);


                    BalanceUpdated.Invoke(this, new WrapperBalanceEvent(unlocked,total ));
                }
            }
            else if (Regex.IsMatch(line, "amount[\\s]+tx"))
            {
                //Transactions.Clear();
                isFetchingTransactions = true;
            }
            //else if (Regex.IsMatch(line, "([0-9]+\\.[0-9]+)[\\s]*([TF])[\\s]*[0-9]+[\\s]*<([0-9a-z]+)>"))
            else if (line.Split(' ').Length > 10)
            {
                if (line.Split(' ')[3].Length == 64)
                {

                    for (int i = 0; i < 40; i++)
                        line = line.Replace("  ", " ");


                    string[] aux = System.Text.RegularExpressions.Regex.Split(line, " ");                                        
                    DateTime date = DateTime.Parse(aux[0] + " " + aux[1]);
                    decimal amount = decimal.Parse(aux[3], CultureInfo.InvariantCulture);
                    decimal fee = decimal.Parse(aux[4], CultureInfo.InvariantCulture);
                    int block = int.Parse(aux[5], CultureInfo.InvariantCulture);
                    bool spent = true;
                    string transactionId = line.Split(' ')[2];
                    if (amount < 0)
                        spent = false;
                    
                    bool exist = false;
                    foreach (var item in Transactions)
                    {
                        exist = item.TransactionId == transactionId;
                    }


                    if (!exist)
                    {
                        var transaction = new Transaction(date,amount, spent, transactionId,block,fee);
                        Transactions.Add(transaction);
                    }
                    else
                    {
                        //if (spent)
                        //{
                        //    existingTransaction.Amount -= amount;
                        //}
                        //else
                        //{
                        //    existingTransaction.Amount += amount;
                        //}

                        //if (existingTransaction.Amount < 0)
                        //{
                        //    existingTransaction.Availablity = "Unavailable";
                        //}
                        //else
                        //{
                        //    existingTransaction.Availablity = "Available";
                        //}
                    }

                    isFetchingTransactions = true;
                }
            }
            else if (line.Contains("No incoming transfers")
                || line.Contains("Starting refresh...")
                || string.IsNullOrWhiteSpace(line))
            {
                // Ignore these lines
            }
            else
            {
                lineIsHandled = false; 
            }

            if (!isFetchingTransactions && TransactionsFetched != null)
            {
                foreach (var transaction in Transactions)
                {
                    transaction.Amount = Math.Abs(transaction.Amount);
                }

                TransactionsFetched.Invoke(this, new WrapperEvent<IList<Transaction>>(Transactions));
            }

            base.HandleLine(line, isError, lineIsHandled);

            beforeLine = line;
        }

        private void SetWalletReadyToSpent(bool readyToSpent)
        {
            if (WalletReadyToSpent != null)
            {
                WalletReadyToSpent.Invoke(this, new WrapperEvent<bool>(readyToSpent));
            }
        }
    }
}
