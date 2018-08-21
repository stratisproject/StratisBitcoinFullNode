using System;
using System.Collections.Generic;
using NBitcoin;

namespace Stratis.SmartContracts.Core.Receipts
{
    /// <summary>
    /// Used to store user-defined logs. 
    /// </summary>
    public class Log
    {
        public uint160 Address { get; }
        public IEnumerable<byte[]> Topics { get; }
        public byte[] Data { get; }

        public Log(uint160 address, IEnumerable<byte[]> topics, byte[] data)
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
            throw new NotImplementedException("To be built with the introduction of logs to contracts");
        }

        public static Log FromBytesRlp(byte[] bytes)
        {
            throw new NotImplementedException("To be built with the introduction of logs to contracts");
        }
    }
}
