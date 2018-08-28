using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
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

        /// <summary>Header chain.</summary>
        private readonly ConcurrentChain chainState;

        /// <summary>Repository containing blocks.</summary>
        private readonly IBlockRepository blockRepository;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// Constructs a wallet fee policy.
        /// </summary>
        /// <param name="nodeSettings">Settings for the the node.</param>
        /// <param name="chain">Header chain.</param>
        /// <param name="blockRepository">Repository containing blocks.</param>
        /// <param name="loggerFactory">Factory for creating instance logger.</param>
        public WalletFeePolicy( NodeSettings nodeSettings,                                 
                                ConcurrentChain chain,
                                IBlockRepository blockRepository,
                                ILoggerFactory loggerFactory)
        {
            this.minTxFee = nodeSettings.MinTxFeeRate;
            this.fallbackFee = nodeSettings.FallbackTxFeeRate;
            this.payTxFee = new FeeRate(0);
            this.maxTxFee = new Money(0.1M, MoneyUnit.BTC);
            this.minRelayTxFee = nodeSettings.MinRelayTxFeeRate;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.chainState = chain;
            this.blockRepository = blockRepository;
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
            this.logger.LogTrace("({0}:{1},{2}:{3},{4}:{5})", nameof(txBytes), txBytes, nameof(confirmTarget), confirmTarget, nameof(targetFee), targetFee);

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

            this.logger.LogTrace("():{0}", nFeeNeeded);
            return nFeeNeeded;
        }

        /// <inheritdoc />
        public FeeRate GetFeeRate(int confirmTarget)
        {
            this.logger.LogTrace("({0}:{1})", nameof(confirmTarget), confirmTarget);

            // Maximum number of blocks to check empty state.
            const int maxBlockCount = 3;

            // Minimum number of transactions required in a block to consider it not empty.
            const int minTxCount = 5;

            //this.blockPolicyEstimator.EstimateSmartFee(confirmTarget, this.mempool, out estimateFoundTarget).GetFee(txBytes);
            FeeRate feeRate = this.AreBlocksEmptyAsync(maxBlockCount, minTxCount).GetAwaiter().GetResult() ?  this.minRelayTxFee : this.fallbackFee;
            this.logger.LogTrace("():{0}", feeRate);
            return feeRate;
        }

        /// <summary>
        /// Identifies whether the last <paramref name="maxBlockCount"/> blocks are empty
        /// based on blocks transaction count being greater than <paramref name="minTxCount"/> 
        /// to determine if they are empty are not.
        /// </summary>
        /// <param name="maxBlockCount">Number of blocks to check.</param>
        /// <param name="minTxCount">Minimum number of transaction in a block to consider it not empty.</param>
        /// <returns>Whether recent blocks are empty.</returns>
        private async Task<bool> AreBlocksEmptyAsync(int maxBlockCount, int minTxCount)
        {
            this.logger.LogTrace("({0}:{1},{2}:{3})", nameof(maxBlockCount), maxBlockCount, nameof(minTxCount), minTxCount);
            var headers = this.chainState.ToEnumerable(true);
            
            int blockCount = 0;

            bool isBlockEmpty = true;
            foreach (ChainedHeader header in headers)
            {
                if (blockCount >= maxBlockCount || !isBlockEmpty)
                    break;

                Block block = header.Block;
                if (block == null)
                    block = await this.blockRepository.GetAsync(header.HashBlock);

                if (block != null)
                {
                    if (block.Transactions.Count >= minTxCount)
                        isBlockEmpty = false;

                    blockCount++;
                }
            }

            this.logger.LogTrace("():{0}", isBlockEmpty);
            return isBlockEmpty;
        }
    }
}
