using System.IO;
using NBitcoin.Protocol;

namespace NBitcoin
{
    public interface IBitcoinSerializable
    {
        void ReadWrite(BitcoinStream stream);
    }

    public interface IHaveNetworkOptions
    {
        NetworkOptions GetNetworkOptions();
    }

    public static class BitcoinSerializableExtensions
    {
        public static void ReadWrite(this IBitcoinSerializable serializable, Stream stream, bool serializing, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION, 
            NetworkOptions options = null)
        {
            // If no options have been provided then take the options from the serializable
            if (options == null && serializing && serializable is IHaveNetworkOptions)
                options = (serializable as IHaveNetworkOptions).GetNetworkOptions();

            serializable.ReadWrite(new BitcoinStream(stream, serializing)
            {
                ProtocolVersion = version,
                TransactionOptions = options ?? NetworkOptions.TemporaryOptions
            });
        }
        public static int GetSerializedSize(this IBitcoinSerializable serializable, ProtocolVersion version, SerializationType serializationType)
        {
            BitcoinStream s = new BitcoinStream(Stream.Null, true);
            s.Type = serializationType;
            s.ReadWrite(serializable);
            return (int)s.Counter.WrittenBytes;
        }
        public static int GetSerializedSize(this IBitcoinSerializable serializable, NetworkOptions options)
        {
            var bms = new BitcoinStream(Stream.Null, true);
            bms.TransactionOptions = options;
            serializable.ReadWrite(bms);
            return (int)bms.Counter.WrittenBytes;
        }
        public static int GetSerializedSize(this IBitcoinSerializable serializable, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION)
        {
            return GetSerializedSize(serializable, version, SerializationType.Disk);
        }

        public static string ToHex(this IBitcoinSerializable serializable, SerializationType serializationType = SerializationType.Disk)
        {
            using (var memoryStream = new MemoryStream())
            {
                BitcoinStream bitcoinStream = new BitcoinStream(memoryStream, true);
                bitcoinStream.Type = serializationType;
                bitcoinStream.ReadWrite(serializable);
                memoryStream.Seek(0, SeekOrigin.Begin);
                var bytes = memoryStream.ReadBytes((int)memoryStream.Length);
                return DataEncoders.Encoders.Hex.EncodeData(bytes);
            }
        }

        public static void ReadWrite(this IBitcoinSerializable serializable, byte[] bytes, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION, 
            NetworkOptions options = null)
        {
            ReadWrite(serializable, new MemoryStream(bytes), false, version, options);
        }

        public static void FromBytes(this IBitcoinSerializable serializable, byte[] bytes, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION, 
            NetworkOptions options = null)
        {
            var bms = new BitcoinStream(bytes)
            {
                ProtocolVersion = version,
                TransactionOptions = options
            };
            serializable.ReadWrite(bms);
        }

        public static T Clone<T>(this T serializable, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION, NetworkOptions options = null) where T : IBitcoinSerializable, new()
        {
            options = options ?? NetworkOptions.TemporaryOptions;
            var instance = new T();
            if (serializable is IHaveNetworkOptions haveNetworkOptions)
                options = haveNetworkOptions.GetNetworkOptions();
            instance.FromBytes(serializable.ToBytes(version, options), version, options);
            return instance;
        }
        
        public static byte[] ToBytes(this IBitcoinSerializable serializable, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION,
            NetworkOptions options = null)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                var bms = new BitcoinStream(ms, true)
                {
                    ProtocolVersion = version,
                    // If no options have been provided then take the options from the serializable (or default)
                    TransactionOptions = options ?? ((serializable as IHaveNetworkOptions)?.GetNetworkOptions() ?? NetworkOptions.TemporaryOptions)
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
