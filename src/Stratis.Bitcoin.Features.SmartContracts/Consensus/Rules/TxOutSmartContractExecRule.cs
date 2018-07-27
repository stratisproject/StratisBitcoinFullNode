using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.MemoryPool;
using SmartContractScript = Stratis.SmartContracts.Core.SmartContractScript;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules
{
    /// <summary>
    /// Each transaction should have only 1 'SmartContractExec' output
    /// </summary>
    [PartialValidationRule]
    public class TxOutSmartContractExecRule : ConsensusRule, ISmartContractMempoolRule
    {
        public override Task RunAsync(RuleContext context)
        {
            Block block = context.ValidationContext.Block;

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
            var smartContractExecCount = transaction.Outputs.Count(o => SmartContractScript.IsSmartContractExec(o.ScriptPubKey));
            if (smartContractExecCount > 1)
                new ConsensusError("multiple-smartcontractexec-outputs", "transaction contains multiple smartcontractexec outputs").Throw();
        }
    }
}