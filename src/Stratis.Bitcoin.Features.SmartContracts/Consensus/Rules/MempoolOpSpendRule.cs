using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules
{
    public class MempoolOpSpendRule : ISmartContractMempoolRule
    {
        public void CheckTransaction(Transaction transaction)
        {
            if (transaction.IsSmartContractSpendTransaction())
                Throw();
        }

        private void Throw()
        {
            // TODO make nicer
            new ConsensusError("opspend-in-mempool", "opspend shouldn't be in mempool").Throw();
        }
    }
}
