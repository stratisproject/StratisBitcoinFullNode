using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    public class CheckTransactionRule : ConsensusRule
    {
        public override Task RunAsync(ContextInformation context)
        {
            Block block = context.BlockValidationContext.Block;
            var options = context.Consensus.Option<PowConsensusOptions>();

            // Check transactions
            foreach (Transaction tx in block.Transactions)
                this.CheckTransaction(options, tx);

            return Task.CompletedTask;
        }

        public virtual void CheckTransaction(PowConsensusOptions options, Transaction tx)
        {
            this.Logger.LogTrace("()");

            // Basic checks that don't depend on any context.
            if (tx.Inputs.Count == 0)
            {
                this.Logger.LogTrace("(-)[TX_NO_INPUT]");
                ConsensusErrors.BadTransactionNoInput.Throw();
            }

            if (tx.Outputs.Count == 0)
            {
                this.Logger.LogTrace("(-)[TX_NO_OUTPUT]");
                ConsensusErrors.BadTransactionNoOutput.Throw();
            }

            // Size limits (this doesn't take the witness into account, as that hasn't been checked for malleability).
            if (BlockSizeRule.GetSize(tx, TransactionOptions.None) > options.MaxBlockBaseSize)
            {
                this.Logger.LogTrace("(-)[TX_OVERSIZE]");
                ConsensusErrors.BadTransactionOversize.Throw();
            }

            // Check for negative or overflow output values
            long valueOut = 0;
            foreach (TxOut txout in tx.Outputs)
            {
                if (txout.Value.Satoshi < 0)
                {
                    this.Logger.LogTrace("(-)[TX_OUTPUT_NEGATIVE]");
                    ConsensusErrors.BadTransactionNegativeOutput.Throw();
                }

                if (txout.Value.Satoshi > options.MaxMoney)
                {
                    this.Logger.LogTrace("(-)[TX_OUTPUT_TOO_LARGE]");
                    ConsensusErrors.BadTransactionTooLargeOutput.Throw();
                }

                valueOut += txout.Value;
                if (!this.MoneyRange(options, valueOut))
                {
                    this.Logger.LogTrace("(-)[TX_TOTAL_OUTPUT_TOO_LARGE]");
                    ConsensusErrors.BadTransactionTooLargeTotalOutput.Throw();
                }
            }

            // Check for duplicate inputs.
            HashSet<OutPoint> inOutPoints = new HashSet<OutPoint>();
            foreach (TxIn txin in tx.Inputs)
            {
                if (inOutPoints.Contains(txin.PrevOut))
                {
                    this.Logger.LogTrace("(-)[TX_DUP_INPUTS]");
                    ConsensusErrors.BadTransactionDuplicateInputs.Throw();
                }

                inOutPoints.Add(txin.PrevOut);
            }

            if (tx.IsCoinBase)
            {
                if ((tx.Inputs[0].ScriptSig.Length < 2) || (tx.Inputs[0].ScriptSig.Length > 100))
                {
                    this.Logger.LogTrace("(-)[BAD_COINBASE_SIZE]");
                    ConsensusErrors.BadCoinbaseSize.Throw();
                }
            }
            else
            {
                foreach (TxIn txin in tx.Inputs)
                {
                    if (txin.PrevOut.IsNull)
                    {
                        this.Logger.LogTrace("(-)[TX_NULL_PREVOUT]");
                        ConsensusErrors.BadTransactionNullPrevout.Throw();
                    }
                }
            }
        }

        private bool MoneyRange(PowConsensusOptions options, long nValue)
        {
            return ((nValue >= 0) && (nValue <= options.MaxMoney));
        }
    }
}