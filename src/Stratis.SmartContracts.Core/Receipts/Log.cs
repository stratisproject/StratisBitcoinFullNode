using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Nethereum.RLP;

namespace Stratis.SmartContracts.Core.Receipts
{
    /// <summary>
    /// Used to store user-defined logs. 
    /// </summary>
    public class Log
    {
        public uint160 Address { get; }
        public IList<byte[]> Topics { get; }
        public byte[] Data { get; }

        public Log(uint160 address, IList<byte[]> topics, byte[] data)
        {
            this.Address = address;
            this.Topics = topics;
            this.Data = data;
        }

        /// <summary>
        /// Return a bloom filter for the address of the contract logging and the topics to be logged.
        /// </summary>
        public Bloom GetBloom()
        {
            var bloom = new Bloom();
            bloom.Add(this.Address.ToBytes());
            foreach(byte[] topic in this.Topics)
            {
                bloom.Add(topic);
            }
            return bloom;
        }

        public byte[] ToBytesRlp()
        {
            IList<byte[]> encodedTopics = this.Topics.Select(x => RLP.EncodeElement(x)).ToList();

            return RLP.EncodeList(
                RLP.EncodeElement(this.Address.ToBytes()),
                RLP.EncodeElement(RLP.EncodeList(encodedTopics.ToArray())),
                RLP.EncodeElement(this.Data)
            );
        }

        public static Log FromBytesRlp(byte[] bytes)
        {
            RLPCollection list = RLP.Decode(bytes);
            RLPCollection innerList = (RLPCollection)list[0];

            RLPCollection topicList = RLP.Decode(innerList[1].RLPData);
            RLPCollection innerTopicList = (RLPCollection)topicList[0];
            IList<byte[]> topics = innerTopicList.Select(x => x.RLPData).ToList();

            return new Log(
                new uint160(innerList[0].RLPData),
                topics,
                innerList[2].RLPData
            );
        }
    }
}
