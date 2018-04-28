using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules
{
    /// <summary>
    /// Checks that transactsions sent to the mempool don't include the OP_SPEND opcode.
    /// </summary>
    public class MempoolOpSpendRule : ISmartContractMempoolRule
    {
        public void CheckTransaction(MempoolValidationContext context)
        {
            if (context.Transaction.IsSmartContractSpendTransaction())
                Throw();
        }

        private void Throw()
        {
            // TODO make nicer
            new ConsensusError("opspend-in-mempool", "opspend shouldn't be in mempool").Throw();
        }
    }
}
