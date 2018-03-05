using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BloodDonationCoin.Core
{
    public class Transaction
    {
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public decimal Fee { get; set; }
        public string Availablity { get; set; }
        public string TransactionId { get; set; }
        public int Block{ get; set; }

        public Transaction(DateTime date, decimal amount, bool spent, string transactionId,int block,decimal fee)
        {
            Date = date;
            Amount = amount;
            this.Availablity = spent ? "Receive" : "Transfer";
            TransactionId = transactionId;
            Block = block;
            Fee = fee;
        }
    }
}
