using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.MemoryPool;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules
{
    /// <summary>
    /// Each transaction should have only 1 'SmartContractExec' output
    /// </summary>
    [ValidationRule(CanSkipValidation = false)]
    public class TxOutSmartContractExecRule : ConsensusRule, ISmartContractMempoolRule
    {
        public override Task RunAsync(RuleContext context)
        {
            Block block = context.BlockValidationContext.Block;

            foreach (Transaction transaction in block.Transactions)
            {
                CheckTransaction(transaction);
            }

            return Task.CompletedTask;
        }

        public void CheckTransaction(MempoolValidationContext context)
        {
            CheckTransaction(context.Transaction);
        }

        private void CheckTransaction(Transaction transaction)
        {
            var smartContractExecCount = transaction.Outputs.Count(o => o.ScriptPubKey.IsSmartContractExec);

            if (smartContractExecCount > 1)
            {
                // TODO make nicer
                new ConsensusError("multiple-smartcontractexec-outputs",
                    "transaction contains multiple smartcontractexec outputs").Throw();
            }
        }
    }
}