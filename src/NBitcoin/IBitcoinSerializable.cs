using System.IO;
using NBitcoin.Protocol;

namespace NBitcoin
{
    public interface IBitcoinSerializable
    {
        void ReadWrite(BitcoinStream stream);
    }

    public static class BitcoinSerializableExtensions
    {
        public static void ReadWrite(this IBitcoinSerializable serializable, Stream stream, bool serializing, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION, Network network = null)
        {
            network = network ?? Network.Main;
            
            serializable.ReadWrite(new BitcoinStream(stream, serializing)
            {
                ProtocolVersion = version,
                ConsensusFactory = network.Consensus.ConsensusFactory
            });
        }
        public static int GetSerializedSize(this IBitcoinSerializable serializable, ProtocolVersion version, SerializationType serializationType)
        {
            BitcoinStream s = new BitcoinStream(Stream.Null, true);
            s.Type = serializationType;
            s.ReadWrite(serializable);
            return (int)s.Counter.WrittenBytes;
        }
        public static int GetSerializedSize(this IBitcoinSerializable serializable, TransactionOptions options)
        {
            var bms = new BitcoinStream(Stream.Null, true);
            bms.TransactionOptions = options;
            serializable.ReadWrite(bms);
            return (int)bms.Counter.WrittenBytes;
        }

        public static string ToHex(this IBitcoinSerializable serializable, Network network, SerializationType serializationType = SerializationType.Disk)
        {
            using (var memoryStream = new MemoryStream())
            {
                BitcoinStream bitcoinStream = new BitcoinStream(memoryStream, true);
                bitcoinStream.ConsensusFactory = network.Consensus.ConsensusFactory;

                bitcoinStream.Type = serializationType;
                bitcoinStream.ReadWrite(serializable);
                memoryStream.Seek(0, SeekOrigin.Begin);
                var bytes = memoryStream.ReadBytes((int)memoryStream.Length);
                return DataEncoders.Encoders.Hex.EncodeData(bytes);
            }
        }

        public static int GetSerializedSize(this IBitcoinSerializable serializable, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION)
        {
            return GetSerializedSize(serializable, version, SerializationType.Disk);
        }

        public static void ReadWrite(this IBitcoinSerializable serializable, byte[] bytes, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION, Network network = null)
        {
            ReadWrite(serializable, new MemoryStream(bytes), false, version, network);
        }

        public static void FromBytes(this IBitcoinSerializable serializable, byte[] bytes, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION, Network network = null)
        {
            network = network ?? Network.Main;
            
            var bms = new BitcoinStream(bytes)
            {
                ProtocolVersion = version,
                ConsensusFactory = network.Consensus.ConsensusFactory
            };
            serializable.ReadWrite(bms);
        }

        public static T Clone<T>(this T serializable, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION, Network network = null) where T : IBitcoinSerializable, new()
        {
            network = network ?? Network.Main;
            
            if (!network.Consensus.ConsensusFactory.TryCreateNew<T>(out T instance))
                instance = new T();

            instance.FromBytes(serializable.ToBytes(version, network), version, network);
            return instance;
        }

        public static byte[] ToBytes(this IBitcoinSerializable serializable, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION, Network network = null)
        {
            network = network ?? Network.Main;

            using (MemoryStream ms = new MemoryStream())
            {
                var bms = new BitcoinStream(ms, true)
                {
                    ProtocolVersion = version,
                    ConsensusFactory = network.Consensus.ConsensusFactory
                };
                serializable.ReadWrite(bms);
                return ToArrayEfficient(ms);
            }
        }

        public static byte[] ToArrayEfficient(this MemoryStream ms)
        {
#if !(PORTABLE || NETCORE)
            var bytes = ms.GetBuffer();
            Array.Resize(ref bytes, (int)ms.Length);
            return bytes;
#else
            return ms.ToArray();
#endif
        }
    }
}
