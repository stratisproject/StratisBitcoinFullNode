namespace NBitcoin.Protocol
{
    public class BitcoinSerializablePayload<T> : Payload where T : IBitcoinSerializable, new()
    {
        private T obj = new T();
        public T Obj { get { return this.obj; } set { this.obj = value; } }

        public BitcoinSerializablePayload()
        {
        }

        public BitcoinSerializablePayload(T obj)
        {
            this.obj = obj;
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.obj);
        }
    }
}