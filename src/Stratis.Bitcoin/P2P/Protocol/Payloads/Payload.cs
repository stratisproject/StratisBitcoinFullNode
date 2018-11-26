using System.Reflection;
using NBitcoin;
using TracerAttributes;

namespace Stratis.Bitcoin.P2P.Protocol.Payloads
{
    public class Payload : IBitcoinSerializable
    {
        public virtual string Command
        {
            get
            {
                return this.GetType().GetCustomAttribute<PayloadAttribute>().Name;
            }
        }

        [NoTrace]
        public void ReadWrite(BitcoinStream stream)
        {
            using (stream.SerializationTypeScope(SerializationType.Network))
            {
                this.ReadWriteCore(stream);
            }
        }

        [NoTrace]
        public virtual void ReadWriteCore(BitcoinStream stream)
        {
        }

        [NoTrace]
        public override string ToString()
        {
            return this.GetType().Name;
        }
    }
}
