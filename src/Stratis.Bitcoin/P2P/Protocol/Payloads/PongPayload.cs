using NBitcoin;

namespace Stratis.Bitcoin.P2P.Protocol.Payloads
{
    [Payload("pong")]
    public class PongPayload : Payload
    {
        private ulong nonce;

        public ulong Nonce { get { return this.nonce; } set { this.nonce = value; } }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.nonce);
        }

        public override string ToString()
        {
            return base.ToString() + " : " + this.Nonce;
        }
    }
}