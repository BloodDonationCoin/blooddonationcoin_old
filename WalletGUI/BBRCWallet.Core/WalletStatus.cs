﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BloodDonationCoin.Core
{
    public enum WalletStatus
    {
        Ready,
        Error,
        SynchronizingWallet,
        SynchronizingBlockchain
    }
}
