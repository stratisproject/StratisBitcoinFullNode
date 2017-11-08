namespace NBitcoin.Protocol
{
    [Payload("filteradd")]
    public class FilterAddPayload : Payload
    {
        private byte[] data;
        public byte[] Data
        {
            get
            {
                return this.data;
            }
            set
            {
                this.data = value;
            }
        }

        public FilterAddPayload()
        {
        }

        public FilterAddPayload(byte[] data)
        {
            this.data = data;
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWriteAsVarString(ref this.data);
        }
    }
}
