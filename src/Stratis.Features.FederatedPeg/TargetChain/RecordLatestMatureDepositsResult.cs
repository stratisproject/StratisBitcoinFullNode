using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    public sealed class RecordLatestMatureDepositsResult
    {
        public RecordLatestMatureDepositsResult()
        {
            this.WithDrawalTransactions = new List<Transaction>();
        }

        public bool MatureDepositRecorded { get; private set; }
        public List<Transaction> WithDrawalTransactions { get; private set; }

        public RecordLatestMatureDepositsResult Failed()
        {
            this.MatureDepositRecorded = false;
            return this;
        }

        public RecordLatestMatureDepositsResult Succeeded()
        {
            this.MatureDepositRecorded = true;
            return this;
        }

        public RecordLatestMatureDepositsResult Pending()
        {
            var result = new RecordLatestMatureDepositsResult();
            return result;
        }
    }
}
