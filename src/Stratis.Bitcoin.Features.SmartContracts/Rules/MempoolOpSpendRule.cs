using System.Linq;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.SmartContracts.Core;

namespace Stratis.Bitcoin.Features.SmartContracts.Rules
{
    /// <summary>
    /// Checks that transactions sent to the mempool don't include the OP_SPEND opcode.
    /// </summary>
    public class MempoolOpSpendRule : ISmartContractMempoolRule
    {
        public void CheckTransaction(MempoolValidationContext context)
        {
            if (context.Transaction.Inputs.Any(x => x.ScriptSig.IsSmartContractSpend()) || context.Transaction.Outputs.Any(x => x.ScriptPubKey.IsSmartContractSpend()))
                this.Throw();
        }

        private void Throw()
        {
            new ConsensusError("opspend-in-mempool", "opspend shouldn't be in transactions created by users").Throw();
        }
    }
}