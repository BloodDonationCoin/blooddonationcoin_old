using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BloodDonationCoin.Core
{
    public class WrapperEvent<T> : EventArgs
    {
        public T Data { get; set; }

        public WrapperEvent(T data)
        {
            Data = data;
        }
    }
}
