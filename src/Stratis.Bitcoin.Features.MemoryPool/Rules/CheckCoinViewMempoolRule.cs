using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.MemoryPool.Rules
{
    /// <summary>
    /// Validates the transaction with the coin view.
    /// Checks if already in coin view, and missing and unavailable inputs.
    /// </summary>
    public class CheckCoinViewMempoolRule : MempoolRule
    {
        public CheckCoinViewMempoolRule(Network network,
            ITxMempool mempool,
            MempoolSettings mempoolSettings,
            ChainIndexer chainIndexer,
            ILoggerFactory loggerFactory) : base(network, mempool, mempoolSettings, chainIndexer, loggerFactory)
        {
        }

        public override void CheckTransaction(MempoolValidationContext context)
        {
            Guard.Assert(context.View != null);

            context.LockPoints = new LockPoints();

            // Do we already have it?
            if (context.View.HaveCoins(context.TransactionHash))
            {
                this.logger.LogTrace("(-)[INVALID_ALREADY_KNOWN]");
                context.State.Invalid(MempoolErrors.AlreadyKnown).Throw();
            }

            // Do all inputs exist?
            // Note that this does not check for the presence of actual outputs (see the next check for that),
            // and only helps with filling in pfMissingInputs (to determine missing vs spent).
            foreach (TxIn txin in context.Transaction.Inputs)
            {
                if (!context.View.HaveCoins(txin.PrevOut.Hash))
                {
                    context.State.MissingInputs = true;
                    this.logger.LogTrace("(-)[FAIL_MISSING_INPUTS]");
                    context.State.Fail(MempoolErrors.MissingInputs).Throw(); // fMissingInputs and !state.IsInvalid() is used to detect this condition, don't set state.Invalid()
                }

                UnspentOutputs coins = context.View.GetCoins(txin.PrevOut.Hash);
                // Check if we are prematurely spending a coinbase transaction.
                // We use tip + 1 because the earliest the mempool transaction can appear in a block would be tipHeight + 1.
                if (coins.IsCoinbase && ((this.chainIndexer.Height + 1 - coins.Height) < this.network.Consensus.CoinbaseMaturity))
                {
                    context.State.Invalid(MempoolErrors.PrematureCoinbase).Throw();
                }

                // Check if we are prematurely spending a coinstake transaction.
                // The minimum maturity for coinstakes was softforked to be higher than the corresponding coinbase maturity.
                if (this.network.Consensus.IsProofOfStake && coins.IsCoinstake)
                {
                    var options = (PosConsensusOptions)this.network.Consensus.Options;
                    int minConf = options.GetStakeMinConfirmations(this.chainIndexer.Height, this.network);

                    if ((this.chainIndexer.Height + 1 - coins.Height) < minConf)
                        context.State.Invalid(MempoolErrors.PrematureCoinstake).Throw();
                }
            }

            // Are the actual inputs available?
            if (!context.View.HaveInputs(context.Transaction))
            {
                this.logger.LogTrace("(-)[INVALID_BAD_INPUTS]");
                context.State.Invalid(MempoolErrors.BadInputsSpent).Throw();
            }
        }
    }
}