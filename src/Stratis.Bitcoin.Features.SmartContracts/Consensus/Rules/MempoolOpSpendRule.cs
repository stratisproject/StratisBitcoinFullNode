using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules
{
    /// <summary>
    /// Checks that transactions sent to the mempool don't include the OP_SPEND opcode.
    /// </summary>
    public class MempoolOpSpendRule : ISmartContractMempoolRule
    {
        public void CheckTransaction(MempoolValidationContext context)
        {
            if (context.Transaction.Inputs.Any(x => ContainsOpSpend(x.ScriptSig)) || context.Transaction.Outputs.Any(x => ContainsOpSpend(x.ScriptPubKey)))
                Throw();
        }

        private static bool ContainsOpSpend(Script script)
        {
            return script.ToOps().Any(x => x.Code == OpcodeType.OP_SPEND);
        }

        private void Throw()
        {
            new ConsensusError("opspend-in-mempool", "opspend shouldn't be in transactions created by users").Throw();
        }
    }
}