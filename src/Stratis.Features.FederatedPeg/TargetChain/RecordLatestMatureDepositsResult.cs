using NBitcoin;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    public sealed class RecordLatestMatureDepositsResult
    {
        private RecordLatestMatureDepositsResult()
        {
        }

        public bool Succeed { get; private set; }
        public Transaction WithDrawalTransaction { get; internal set; }

        public RecordLatestMatureDepositsResult Failed()
        {
            this.Succeed = false;
            return this;
        }

        public RecordLatestMatureDepositsResult Succeeded()
        {
            this.Succeed = true;
            return this;
        }

        public static RecordLatestMatureDepositsResult Pending()
        {
            var result = new RecordLatestMatureDepositsResult();
            return result;
        }
    }
}
