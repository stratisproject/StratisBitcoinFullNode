using System.Linq;
using System.Threading.Tasks;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules
{
    /// <summary>
    /// Each transaction should have only 1 'SmartContractExec' output
    /// </summary>
    [ValidationRule(CanSkipValidation = false)]
    public class TxOutSmartContractExecRule : ConsensusRule
    {
        public override Task RunAsync(RuleContext context)
        {
            var block = context.BlockValidationContext.Block;

            foreach (var tx in block.Transactions)
            {
                var smartContractExecCount = tx.Outputs.Count(o => o.ScriptPubKey.IsSmartContractExec);

                if (smartContractExecCount > 1)
                {
                    // TODO make nicer
                    new ConsensusError("multiple-smartcontractexec-outputs",
                        "transaction contains multiple smartcontractexec outputs").Throw();
                }
            }

            return Task.CompletedTask;
        }
    }
}