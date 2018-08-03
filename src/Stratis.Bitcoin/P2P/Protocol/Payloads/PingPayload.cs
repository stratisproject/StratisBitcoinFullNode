using NBitcoin;

namespace Stratis.Bitcoin.P2P.Protocol.Payloads
{
    [Payload("ping")]
    public class PingPayload : Payload
    {
        private ulong nonce;

        public ulong Nonce { get { return this.nonce; } set { this.nonce = value; } }

        public PingPayload()
        {
            this.nonce = RandomUtils.GetUInt64();
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.nonce);
        }

        public PongPayload CreatePong()
        {
            return new PongPayload()
            {
                Nonce = this.Nonce
            };
        }

        public override string ToString()
        {
            return base.ToString() + " : " + this.Nonce;
        }
    }
}
