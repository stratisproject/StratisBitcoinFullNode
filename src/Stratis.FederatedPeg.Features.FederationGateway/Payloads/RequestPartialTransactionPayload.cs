using NBitcoin;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    [Payload("partial")]
    public class RequestPartialTransactionPayload : Payload
    {
        private uint256 sessionId = uint256.Zero;
        private Transaction template;
        private Transaction transactionPartial;
        private uint256 bossCard = uint256.Zero;

        public Transaction PartialTransaction => this.transactionPartial;

        public Transaction TemplateTransaction => this.template;

        public uint256 SessionId => this.sessionId;

        public uint256 BossCard => this.bossCard;

        public RequestPartialTransactionPayload(uint256 sessionId, Transaction template)
        {
            this.sessionId = sessionId;
            this.template = template;
        }

        public void AddPartial(Transaction partialTransaction, uint256 bossCard)
        {
            this.transactionPartial = partialTransaction;
            this.bossCard = bossCard;
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.sessionId);
            stream.ReadWrite(ref this.bossCard);
            stream.ReadWrite(ref this.transactionPartial);
            stream.ReadWrite(ref this.template);
        }
    }
}
