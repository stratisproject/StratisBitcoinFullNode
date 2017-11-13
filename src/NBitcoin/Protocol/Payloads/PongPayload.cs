namespace NBitcoin.Protocol
{
    [Payload("pong")]
    public class PongPayload : Payload
    {
        private ulong nonce;
        public ulong Nonce { get { return nonce; } set { nonce = value; } }

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