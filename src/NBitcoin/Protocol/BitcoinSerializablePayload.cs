namespace NBitcoin.Protocol
{
    public class BitcoinSerializablePayload<T> : Payload where T : IBitcoinSerializable, new()
    {
        private T @object = new T();
        public T Object { get { return this.@object; } set { this.@object = value; } }

        public BitcoinSerializablePayload()
        {
        }

        public BitcoinSerializablePayload(T obj)
        {
            this.@object = obj;
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.@object);
        }
    }
}