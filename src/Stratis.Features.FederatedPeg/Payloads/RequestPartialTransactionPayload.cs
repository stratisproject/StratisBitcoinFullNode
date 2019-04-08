using NBitcoin;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Features.FederatedPeg.Payloads
{
    [Payload("partial")]
    public class RequestPartialTransactionPayload : Payload
    {
        private Transaction transactionPartial;
        private uint256 depositId;

        public Transaction PartialTransaction => this.transactionPartial;

        public uint256 DepositId => this.depositId;

        /// <remarks>Needed for deserialization.</remarks>
        public RequestPartialTransactionPayload()
        {
        }

        public RequestPartialTransactionPayload(uint256 depositId)
        {
            this.depositId = depositId;
        }

        public RequestPartialTransactionPayload AddPartial(Transaction partialTransaction)
        {
            this.transactionPartial = partialTransaction;

            return this;
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.depositId);
            stream.ReadWrite(ref this.transactionPartial);
        }

        public override string ToString()
        {
            return $"{nameof(this.Command)}:'{this.Command}',{nameof(this.DepositId)}:'{this.DepositId}'";
        }
    }
}
