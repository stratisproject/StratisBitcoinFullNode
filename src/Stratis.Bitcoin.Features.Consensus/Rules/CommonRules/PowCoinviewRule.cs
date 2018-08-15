﻿using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <inheritdoc />
    public sealed class PowCoinviewRule : CoinViewRule
    {
        /// <summary>Consensus parameters.</summary>
        private IConsensus consensus;

        /// <inheritdoc />
        public override void Initialize()
        {
            this.Logger.LogTrace("()");

            base.Initialize();

            this.consensus = this.Parent.Network.Consensus;

            this.Logger.LogTrace("(-)");
        }

        /// <inheritdoc/>
        protected override bool IsProtocolTransaction(Transaction transaction)
        {
            return transaction.IsCoinBase;
        }

        /// <inheritdoc/>
        public override void CheckBlockReward(RuleContext context, Money fees, int height, Block block)
        {
            this.Logger.LogTrace("()");

            Money blockReward = fees + this.GetProofOfWorkReward(height);
            if (block.Transactions[0].TotalOut > blockReward)
            {
                this.Logger.LogTrace("(-)[BAD_COINBASE_AMOUNT]");
                ConsensusErrors.BadCoinbaseAmount.Throw();
            }

            this.Logger.LogTrace("(-)");
        }

        /// <inheritdoc/>
        public override Money GetProofOfWorkReward(int height)
        {
            if (this.IsPremine(height))
                return this.consensus.PremineReward;

            if (this.consensus.ProofOfWorkReward == 0)
                return 0;

            int halvings = height / this.consensus.SubsidyHalvingInterval;

            // Force block reward to zero when right shift is undefined.
            if (halvings >= 64)
                return 0;

            Money subsidy = this.consensus.ProofOfWorkReward;
            // Subsidy is cut in half every 210,000 blocks which will occur approximately every 4 years.
            subsidy >>= halvings;

            return subsidy;
        }

        /// <inheritdoc/>
        public override void CheckMaturity(UnspentOutputs coins, int spendHeight)
        {
            base.CheckCoinbaseMaturity(coins, spendHeight);
        }

        /// <inheritdoc/>
        public override void UpdateCoinView(RuleContext context, Transaction transaction)
        {
            base.UpdateUTXOSet(context, transaction);
        }

        /// <inheritdoc />
        public override Task RunAsync(RuleContext context)
        {
            return base.RunAsync(context);
        }
    }
}