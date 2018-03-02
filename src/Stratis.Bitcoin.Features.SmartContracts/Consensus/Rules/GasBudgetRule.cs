using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.SmartContracts;
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
                block.Transactions.SmartContractTransactions().ToList();

            if (!smartContractTransactions.Any())
            {
                // No smart contract transactions, nothing to validate
                return Task.CompletedTask;
            }

            foreach (Transaction transaction in smartContractTransactions)
            {
                // The gas budget supplied
                Money suppliedBudget = transaction.TotalOut;
                
                var carrier = SmartContractCarrier.Deserialize(transaction, transaction.Outputs[0]);

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
            new ConsensusError("total-gas-value-greater-than-total-fee",
                "total supplied gas value was greater than total supplied fee value").Throw();
        }
    }
}
