using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Prevent duplicate transactions in the coinbase.
    /// </summary>
    /// <remarks>
    /// More info here https://github.com/bitcoin/bips/blob/master/bip-0030.mediawiki
    /// </remarks>
    [ExecutionRule]
    public class TransactionDuplicationActivationRule : UtxoStoreConsensusRule
    {
        /// <inheritdoc />>
        /// <exception cref="ConsensusErrors.BadTransactionBIP30"> Thrown if BIP30 is not passed.</exception>
        public override Task RunAsync(RuleContext context)
        {
            if (!context.SkipValidation)
            {
                Block block = context.BlockValidationContext.Block;
                DeploymentFlags flags = context.Flags;
                UnspentOutputSet view = context.Set;

                if (flags.EnforceBIP30)
                {
                    foreach (Transaction tx in block.Transactions)
                    {
                        UnspentOutputs coins = view.AccessCoins(tx.GetHash());
                        if ((coins != null) && !coins.IsPrunable)
                        {
                            this.Logger.LogTrace("(-)[BAD_TX_BIP_30]");
                            ConsensusErrors.BadTransactionBIP30.Throw();
                        }
                    }
                }
            }
            else this.Logger.LogTrace("BIP30 validation skipped for checkpointed block at height {0}.", context.BlockValidationContext.ChainedBlock.Height);

            return Task.CompletedTask;
        }
    }
}