using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>Validate a PoW transaction.</summary>
    public class CheckPowTransactionRule : PartialValidationConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BadTransactionNoInput">Thrown if transaction has no inputs.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionNoOutput">Thrown if transaction has no outputs.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionOversize">Thrown if transaction size is greater than maximum allowed size of a block.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionNegativeOutput">Thrown if at least one transaction output has negative value.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionTooLargeOutput">Thrown if at least one transaction output value is greater than maximum allowed one.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionTooLargeTotalOutput">Thrown if sum of all transaction outputs is greater than maximum allowed one.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionDuplicateInputs">Thrown if any of transaction inputs are duplicate.</exception>
        /// <exception cref="ConsensusErrors.BadCoinbaseSize">Thrown if coinbase transaction is too small or too big.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionNullPrevout">Thrown if transaction contains a null prevout.</exception>
        public override Task RunAsync(RuleContext context)
        {
            if (context.SkipValidation)
                return Task.CompletedTask;

            Block block = context.ValidationContext.BlockToValidate;
            var options = this.Parent.Network.Consensus.Options;

            // Check transactions
            foreach (Transaction tx in block.Transactions)
                this.CheckTransaction(this.Parent.Network, options, tx);

            return Task.CompletedTask;
        }

        public virtual void CheckTransaction(Network network, ConsensusOptions options, Transaction tx)
        {
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
            if (tx.GetSize(TransactionOptions.None, network.Consensus.ConsensusFactory) > options.MaxBlockBaseSize)
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

                if (txout.Value.Satoshi > network.Consensus.MaxMoney)
                {
                    this.Logger.LogTrace("(-)[TX_OUTPUT_TOO_LARGE]");
                    ConsensusErrors.BadTransactionTooLargeOutput.Throw();
                }

                valueOut += txout.Value;
                if (!this.MoneyRange(network.Consensus, valueOut))
                {
                    this.Logger.LogTrace("(-)[TX_TOTAL_OUTPUT_TOO_LARGE]");
                    ConsensusErrors.BadTransactionTooLargeTotalOutput.Throw();
                }
            }

            // Check for duplicate inputs.
            var inOutPoints = new HashSet<OutPoint>();
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

        private bool MoneyRange(IConsensus consensus, long nValue)
        {
            return ((nValue >= 0) && (nValue <= consensus.MaxMoney));
        }
    }
}