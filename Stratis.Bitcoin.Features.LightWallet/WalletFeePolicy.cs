using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Wallet;
using System;

namespace Stratis.Bitcoin.Features.LightWallet
{

    public class LightWalletFeePolicy : IWalletFeePolicy
    {
        private readonly FeeRate minTxFee;
        private readonly Money maxTxFee;
        private readonly FeeRate highxFeePerKb;
        private readonly FeeRate mediumTxFeePerKb;
        private readonly FeeRate lowTxFeePerKb;

        public LightWalletFeePolicy()
        {
            // when estimating the fee, the fee of each transactions needs to be knowen
            // as this is an estimator on a light wallet we dont have the UTXO set
            // that leaves with few options:
            // - an external service, 
            // - hard code fee per kb
            // - we may even be able to monitor the size of the mempool (mini mempool)
            // (not the entire trx) and try to estimate based on pending count
            // TODO: make fee values confugurable on startup

            this.highxFeePerKb = new FeeRate(385939);
            this.mediumTxFeePerKb = new FeeRate(245347);
            this.lowTxFeePerKb = new FeeRate(171274);
            this.maxTxFee = new Money(0.1M, MoneyUnit.BTC);
            this.minTxFee = new FeeRate(1000);
        }

        public Money GetRequiredFee(int txBytes)
        {
            return Math.Max(this.minTxFee.GetFee(txBytes), MempoolValidator.MinRelayTxFee.GetFee(txBytes));
        }

        public Money GetMinimumFee(int txBytes, int confirmTarget)
        {
            // payTxFee is the user-set global for desired feerate
            return this.GetMinimumFee(txBytes, confirmTarget, this.lowTxFeePerKb.GetFee(txBytes));
        }

        public Money GetMinimumFee(int txBytes, int confirmTarget, Money targetFee)
        {
            Money feeNeeded = targetFee;
            
            feeNeeded = this.lowTxFeePerKb.GetFee(txBytes);
            if (confirmTarget < 50) feeNeeded = this.mediumTxFeePerKb.GetFee(txBytes);
            if (confirmTarget < 20) feeNeeded = this.highxFeePerKb.GetFee(txBytes);

            // prevent user from paying a fee below minRelayTxFee or minTxFee
            feeNeeded = Math.Max(feeNeeded, this.GetRequiredFee(txBytes));
            // But always obey the maximum
            if (feeNeeded > this.maxTxFee)
                feeNeeded = this.maxTxFee;
            return feeNeeded;
        }

        public FeeRate GetFeeRate(int confirmTarget)
        {
            FeeRate feeNeeded = this.minTxFee;
            if (confirmTarget < 50) feeNeeded = this.mediumTxFeePerKb;
            if (confirmTarget < 20) feeNeeded = this.highxFeePerKb;
            return feeNeeded;
        }
    }
}
