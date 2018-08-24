using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    public class CheckSigOpsRule : PartialValidationConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BadBlockSigOps">The block contains more signature check operations than allowed.</exception>
        public override Task RunAsync(RuleContext context)
        {
            if (context.SkipValidation)
                return Task.CompletedTask;

            Block block = context.ValidationContext.BlockToValidate;
            ConsensusOptions options = this.Parent.Network.Consensus.Options;

            long nSigOps = 0;
            foreach (Transaction tx in block.Transactions)
                nSigOps += this.GetLegacySigOpCount(tx);

            if ((nSigOps * options.WitnessScaleFactor) > options.MaxBlockSigopsCost)
            {
                this.Logger.LogTrace("(-)[BAD_BLOCK_SIGOPS]");
                ConsensusErrors.BadBlockSigOps.Throw();
            }

            return Task.CompletedTask;
        }

        private long GetLegacySigOpCount(Transaction tx)
        {
            long nSigOps = 0;
            foreach (TxIn txin in tx.Inputs)
                nSigOps += txin.ScriptSig.GetSigOpCount(false);

            foreach (TxOut txout in tx.Outputs)
                nSigOps += txout.ScriptPubKey.GetSigOpCount(false);

            return nSigOps;
        }
    }
}