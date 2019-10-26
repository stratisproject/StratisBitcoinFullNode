using System;
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
        public static void ReadWrite(this IBitcoinSerializable serializable, Stream stream, bool serializing, ProtocolVersion protocolVersion = ProtocolVersion.PROTOCOL_VERSION)
        {
            serializable.ReadWrite(new BitcoinStream(stream, serializing)
            {
                ProtocolVersion = protocolVersion
            });
        }

        public static void ReadWrite(this IBitcoinSerializable serializable, Stream stream, bool serializing, ConsensusFactory consensusFactory, ProtocolVersion protocolVersion = ProtocolVersion.PROTOCOL_VERSION)
        {
            if (consensusFactory == null)
                throw new ArgumentException("{0} cannot be null", nameof(consensusFactory));

            serializable.ReadWrite(new BitcoinStream(stream, serializing)
            {
                ProtocolVersion = protocolVersion,
                ConsensusFactory = consensusFactory
            });
        }

        public static void ReadWrite(this IBitcoinSerializable serializable, byte[] bytes, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION)
        {
            using (var ms = new MemoryStream(bytes))
            {
                ReadWrite(serializable, ms, false, version);
            }
        }

        public static void ReadWrite(this IBitcoinSerializable serializable, byte[] bytes, ConsensusFactory consensusFactory, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION)
        {
            using (var ms = new MemoryStream(bytes))
            {
                ReadWrite(serializable, ms, false, consensusFactory, version);
            }
        }

        public static int GetSerializedSize(this IBitcoinSerializable serializable, ProtocolVersion version, SerializationType serializationType)
        {
            var bitcoinStream = new BitcoinStream(Stream.Null, true)
            {
                Type = serializationType
            };
            bitcoinStream.ReadWrite(serializable);
            return (int)bitcoinStream.Counter.WrittenBytes;
        }

        public static int GetSerializedSize(this IBitcoinSerializable serializable, TransactionOptions options)
        {
            var bitcoinStream = new BitcoinStream(Stream.Null, true)
            {
                TransactionOptions = options
            };
            serializable.ReadWrite(bitcoinStream);
            return (int)bitcoinStream.Counter.WrittenBytes;
        }

        public static string ToHex(this IBitcoinSerializable serializable, Network network, SerializationType serializationType = SerializationType.Disk)
        {
            using (var memoryStream = new MemoryStream())
            {
                var bitcoinStream = new BitcoinStream(memoryStream, true)
                {
                    ConsensusFactory = network.Consensus.ConsensusFactory,
                    Type = serializationType
                };
                bitcoinStream.ReadWrite(serializable);
                memoryStream.Seek(0, SeekOrigin.Begin);
                byte[] bytes = memoryStream.ReadBytes((int)memoryStream.Length);
                return DataEncoders.Encoders.Hex.EncodeData(bytes);
            }
        }

        public static int GetSerializedSize(this IBitcoinSerializable serializable, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION)
        {
            return GetSerializedSize(serializable, version, SerializationType.Disk);
        }

        public static void FromBytes(this IBitcoinSerializable serializable, byte[] bytes, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION)
        {
            using (var ms = new MemoryStream(bytes))
            {
                var bitcoinStream = new BitcoinStream(ms, false)
                {
                    ProtocolVersion = version
                };

                serializable.ReadWrite(bitcoinStream);
            }
        }

        public static void FromBytes(this IBitcoinSerializable serializable, byte[] bytes, ConsensusFactory consensusFactory, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION)
        {
            if (consensusFactory == null)
                throw new ArgumentException("{0} cannot be null", nameof(consensusFactory));

            using (var ms = new MemoryStream(bytes))
            {
                var bitcoinStream = new BitcoinStream(ms, false)
                {
                    ProtocolVersion = version,
                    ConsensusFactory = consensusFactory
                };

                serializable.ReadWrite(bitcoinStream);
            }
        }

        public static T Clone<T>(this T serializable, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION) where T : IBitcoinSerializable, new()
        {
            T instance = new DefaultConsensusFactory().TryCreateNew<T>();
            if (instance == null)
                instance = new T();

            instance.FromBytes(serializable.ToBytes(version), version);
            return instance;
        }

        public static byte[] ToBytes(this IBitcoinSerializable serializable, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION)
        {
            using (var ms = new MemoryStream())
            {
                var bms = new BitcoinStream(ms, true)
                {
                    ProtocolVersion = version
                };

                serializable.ReadWrite(bms);

                return ToArrayEfficient(ms);
            }
        }

        public static byte[] ToBytes(this IBitcoinSerializable serializable, ConsensusFactory consensusFactory, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION)
        {
            if (consensusFactory == null)
                throw new ArgumentException("{0} cannot be null", nameof(consensusFactory));

            using (var ms = new MemoryStream())
            {
                var bms = new BitcoinStream(ms, true)
                {
                    ProtocolVersion = version,
                    ConsensusFactory = consensusFactory
                };

                serializable.ReadWrite(bms);

                return ToArrayEfficient(ms);
            }
        }

        public static byte[] ToArrayEfficient(this MemoryStream ms)
        {
#if !NETCORE
            var bytes = ms.GetBuffer();
            Array.Resize(ref bytes, (int)ms.Length);
            return bytes;
#else
            return ms.ToArray();
#endif
        }
    }
}