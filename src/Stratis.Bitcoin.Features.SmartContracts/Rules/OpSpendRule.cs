using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using SmartContractScript = Stratis.SmartContracts.Core.SmartContractScript;

namespace Stratis.Bitcoin.Features.SmartContracts.Rules
{
    /// <summary>
    /// If OP_SPEND, check that the transaction before is a contract call
    /// </summary>
    public class OpSpendRule : FullValidationConsensusRule
    {
        public override Task RunAsync(RuleContext context)
        {
            Block block = context.ValidationContext.BlockToValidate;

            IEnumerable<Transaction> opSpendTransactions = block.Transactions.Where(tx =>
                tx.IsSmartContractSpendTransaction());

            foreach (Transaction opSpendTransaction in opSpendTransactions)
            {
                var thisIndex = block.Transactions.IndexOf(opSpendTransaction);

                if (thisIndex <= 0)
                {
                    this.Throw();
                };

                Transaction previousTransaction = block.Transactions[thisIndex - 1];

                // This previously would only check that the contract was a call. However we also have to check for create as inside the constructor
                // we could make a call to another contract and that could send funds!
                var previousWasOpCall = previousTransaction.Outputs.Any(o => SmartContractScript.IsSmartContractExec(o.ScriptPubKey));

                if (!previousWasOpCall)
                {
                    this.Throw();
                }
            }

            return Task.CompletedTask;
        }

        private void Throw()
        {
            new ConsensusError("opspend-did-not-follow-opcall", "transaction contained an op-spend that did not follow an op-call").Throw();
        }
    }
}