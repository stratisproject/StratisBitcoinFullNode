namespace NBitcoin.Protocol
{
    [Payload("sendcmpct")]
    public class SendCmpctPayload : Payload
    {
        private byte preferHeaderAndIDs;
        public bool PreferHeaderAndIDs
        {
            get
            {
                return this.preferHeaderAndIDs == 1;
            }
            set
            {
                this.preferHeaderAndIDs = value ? (byte)1 : (byte)0;
            }
        }

        private ulong version = 1;
        public ulong Version { get { return version; } set { version = value; } }

        public SendCmpctPayload()
        {
        }

        public SendCmpctPayload(bool preferHeaderAndIDs)
        {
            this.PreferHeaderAndIDs = preferHeaderAndIDs;
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.preferHeaderAndIDs);
            stream.ReadWrite(ref this.version);
        }
    }
}