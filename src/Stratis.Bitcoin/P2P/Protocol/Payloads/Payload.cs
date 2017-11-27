using NBitcoin;

namespace Stratis.Bitcoin.P2P.Protocol.Payloads
{
    public class Payload : IBitcoinSerializable
    {
        public virtual string Command
        {
            get
            {
                return PayloadAttribute.GetCommandName(this.GetType());
            }
        }

        public void ReadWrite(BitcoinStream stream)
        {
            using (stream.SerializationTypeScope(SerializationType.Network))
            {
                this.ReadWriteCore(stream);
            }
        }

        public virtual void ReadWriteCore(BitcoinStream stream)
        {
        }

        public override string ToString()
        {
            return this.GetType().Name;
        }
    }
}
