using System;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Stratis.Bitcoin.Features.Wallet
{
    public class WalletFeePolicy : IWalletFeePolicy
    {
        /// <summary>Maximum transaction fee.</summary>
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

        /// <summary>
        /// Constructs a wallet fee policy.
        /// </summary>
        /// <param name="nodeSettings">Settings for the the node.</param>
        public WalletFeePolicy(NodeSettings nodeSettings)
        {
            this.minTxFee = nodeSettings.MinTxFeeRate;
            this.fallbackFee = nodeSettings.FallbackTxFeeRate;
            this.payTxFee = new FeeRate(0);
            this.maxTxFee = new Money(0.1M, MoneyUnit.BTC);
            this.minRelayTxFee = nodeSettings.MinRelayTxFeeRate;
        }

        /// <inheritdoc />
        public void Start()
        {
            return;
        }

        /// <inheritdoc />
        public void Stop()
        {
            return;
        }

        /// <inheritdoc />
        public Money GetRequiredFee(int txBytes)
        {
            return Math.Max(this.minTxFee.GetFee(txBytes), this.minRelayTxFee.GetFee(txBytes));
        }

        /// <inheritdoc />
        public Money GetMinimumFee(int txBytes, int confirmTarget)
        {
            // payTxFee is the user-set global for desired feerate
            return this.GetMinimumFee(txBytes, confirmTarget, this.payTxFee.GetFee(txBytes));
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public FeeRate GetFeeRate(int confirmTarget)
        {
            //this.blockPolicyEstimator.EstimateSmartFee(confirmTarget, this.mempool, out estimateFoundTarget).GetFee(txBytes);
            return this.fallbackFee;
        }
    }
}
