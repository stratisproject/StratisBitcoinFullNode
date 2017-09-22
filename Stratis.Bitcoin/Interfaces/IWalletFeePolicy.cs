using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Interfaces
{
    public interface IWalletFeePolicy
    {
        Task Initialize();
        Money GetRequiredFee(int txBytes);
        Money GetMinimumFee(int txBytes, int confirmTarget);
        Money GetMinimumFee(int txBytes, int confirmTarget, Money targetFee);
        FeeRate GetFeeRate(int confirmTarget);
    }
}
