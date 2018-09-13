using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Transaction lock-time calculations are checked using the median of the last 11 blocks instead of the block's time stamp.
    /// </summary>
    /// <remarks>
    /// More info here https://github.com/bitcoin/bips/blob/master/bip-0113.mediawiki
    /// </remarks>
    public class TransactionLocktimeActivationRule : PartialValidationConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BadTransactionNonFinal">Thrown if one or more transactions are not finalized.</exception>
        public override Task RunAsync(RuleContext context)
        {
            if (context.SkipValidation)
                return Task.CompletedTask;

            DeploymentFlags deploymentFlags = context.Flags;
            int newHeight = context.ValidationContext.ChainedHeaderToValidate.Height;
            Block block = context.ValidationContext.BlockToValidate;

            // Start enforcing BIP113 (Median Time Past) using versionbits logic.
            DateTimeOffset nLockTimeCutoff = deploymentFlags.LockTimeFlags.HasFlag(Transaction.LockTimeFlags.MedianTimePast) ?
                context.ValidationContext.ChainedHeaderToValidate.Previous.GetMedianTimePast() :
                block.Header.BlockTime;

            // Check that all transactions are finalized.
            foreach (Transaction transaction in block.Transactions)
            {
                if (!transaction.IsFinal(nLockTimeCutoff, newHeight))
                {
                    this.Logger.LogTrace("(-)[TX_NON_FINAL]");
                    ConsensusErrors.BadTransactionNonFinal.Throw();
                }
            }

            return Task.CompletedTask;
        }
    }
}