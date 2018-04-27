using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules
{
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
