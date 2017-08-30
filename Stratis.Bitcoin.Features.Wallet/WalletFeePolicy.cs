using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Fee;
using System;

namespace Stratis.Bitcoin.Features.Wallet
{
    public interface IWalletFeePolicy
    {
        Money GetRequiredFee(int txBytes);
        Money GetMinimumFee(int txBytes, int confirmTarget);
        Money GetMinimumFee(int txBytes, int confirmTarget, Money targetFee);
        FeeRate GetFeeRate(int confirmTarget);
    }

    public class WalletFeePolicy : IWalletFeePolicy
    {
        private readonly BlockPolicyEstimator blockPolicyEstimator;
        private readonly IMempoolValidator mempoolValidator;
        private readonly TxMempool mempool;

        private readonly Money maxTxFee;

        /// <summary>
        ///  Fees smaller than this (in satoshi) are considered zero fee (for transaction creation)
        ///  Override with -mintxfee
        /// </summary>
        private readonly FeeRate minTxFee;

        /// <summary>
        ///  If fee estimation does not have enough data to provide estimates, use this fee instead.
        ///  Has no effect if not using fee estimation
        ///  Override with -fallbackfee
        /// </summary>
        private readonly FeeRate fallbackFee;

        /// <summary>
        /// Transaction fee set by the user
        /// </summary>
        private readonly FeeRate payTxFee;

        /// <summary>
        /// Min Relay Tx Fee
        /// </summary>
        private readonly FeeRate minRelayTxFee;

        public WalletFeePolicy(BlockPolicyEstimator blockPolicyEstimator, IMempoolValidator mempoolValidator, TxMempool mempool, Network network)
        {
            this.blockPolicyEstimator = blockPolicyEstimator;
            this.mempoolValidator = mempoolValidator;
            this.mempool = mempool;

            this.minTxFee = new FeeRate(1000);
            this.fallbackFee = new FeeRate(20000);
            this.payTxFee = new FeeRate(0);
            this.maxTxFee = new Money(0.1M, MoneyUnit.BTC);
            this.minRelayTxFee = MempoolValidator.MinRelayTxFee;

            // this is a very very ugly hack
            // the fee constants should be set at the 
            // network level or the consensus options
            if (network.Name.ToLower().Contains("stratis"))
            {
                this.minTxFee = new FeeRate(10000);
                this.fallbackFee = new FeeRate(40000);
                this.payTxFee = new FeeRate(0);
                this.maxTxFee = new Money(0.1M, MoneyUnit.BTC);
                this.minRelayTxFee = new FeeRate(10000);
            }
        }

        public Money GetRequiredFee(int txBytes)
        {
            return Math.Max(this.minTxFee.GetFee(txBytes), this.minRelayTxFee.GetFee(txBytes));
        }

        public Money GetMinimumFee(int txBytes, int confirmTarget)
        {
            // payTxFee is the user-set global for desired feerate
            return this.GetMinimumFee(txBytes, confirmTarget, this.payTxFee.GetFee(txBytes));
        }

        public Money GetMinimumFee(int txBytes, int confirmTarget, Money targetFee)
        {
            Money nFeeNeeded = targetFee;
            // User didn't set: use -txconfirmtarget to estimate...
            if (nFeeNeeded == 0)
            {
                int estimateFoundTarget = confirmTarget;

                // TODO: the fee estimation is not ready for release for now use the fall back fee
                //nFeeNeeded = this.blockPolicyEstimator.EstimateSmartFee(confirmTarget, this.mempool, out estimateFoundTarget).GetFee(txBytes);
                // ... unless we don't have enough mempool data for estimatefee, then use fallbackFee
                if (nFeeNeeded == 0)
                    nFeeNeeded = this.fallbackFee.GetFee(txBytes);
            }
            // prevent user from paying a fee below minRelayTxFee or minTxFee
            nFeeNeeded = Math.Max(nFeeNeeded, this.GetRequiredFee(txBytes));
            // But always obey the maximum
            if (nFeeNeeded > this.maxTxFee)
                nFeeNeeded = this.maxTxFee;
            return nFeeNeeded;
        }

        public FeeRate GetFeeRate(int confirmTarget)
        {
            //this.blockPolicyEstimator.EstimateSmartFee(confirmTarget, this.mempool, out estimateFoundTarget).GetFee(txBytes);
            return this.fallbackFee;
        }
    }
}
