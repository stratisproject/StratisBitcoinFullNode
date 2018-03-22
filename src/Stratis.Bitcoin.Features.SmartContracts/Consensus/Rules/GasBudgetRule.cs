using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.SmartContracts.Core;
using Block = NBitcoin.Block;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules
{
    /// <summary>
    /// Validates that the supplied transaction satoshis are greater than the gas budget satoshis in the contract invocation
    /// </summary>
    [ValidationRule(CanSkipValidation = false)]
    public class GasBudgetRule : ConsensusRule
    {
        public override Task RunAsync(RuleContext context)
        {
            Block block = context.BlockValidationContext.Block;

            IEnumerable<Transaction> smartContractTransactions =
                block.Transactions.GetSmartContractExecTransactions().ToList();

            if (!smartContractTransactions.Any())
            {
                // No smart contract transactions, nothing to validate
                return Task.CompletedTask;
            }

            foreach (Transaction transaction in smartContractTransactions)
            {
                // The gas budget supplied
                Money suppliedBudget = transaction.TotalOut;

                // This should never be null as every tx in smartContractTransactions contains a SmartContractOutput
                // So throw if null, because we really didn't expect that
                TxOut smartContractOutput = transaction.Outputs.First(txOut => txOut.ScriptPubKey.IsSmartContractExec);

                var carrier = SmartContractCarrier.Deserialize(transaction, smartContractOutput);

                if (suppliedBudget < new Money(carrier.GasCostBudget))
                {
                    // Supplied satoshis are less than the budget we said we had for the contract execution
                    this.Throw();
                }
            }

            return Task.CompletedTask;
        }

        private void Throw()
        {
            // TODO make nicer
            new ConsensusError("total-gas-value-greater-than-total-fee", "total supplied gas value was greater than total supplied fee value").Throw();
        }
    }
}