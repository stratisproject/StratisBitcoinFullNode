using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Interfaces
{
    public interface IWalletFeePolicy
    {
        void Start();
        void Stop();
        Money GetRequiredFee(int txBytes);
        Money GetMinimumFee(int txBytes, int confirmTarget);
        Money GetMinimumFee(int txBytes, int confirmTarget, Money targetFee);
        FeeRate GetFeeRate(int confirmTarget);
    }
}
