using System;
using NBitcoin;

namespace Stratis.SmartContracts.Core.Receipts
{
    /// <summary>
    /// Will be used to store user-defined logs. 
    /// Currently only a stub for consensus parameters. Subject to change.
    /// </summary>
    public class Log
    {
        public uint160 Address { get; }
        public string Topic { get; }
        public byte[] Data { get; }

        public Log(uint160 address, string topic, byte[] data)
        {
            this.Address = address;
            this.Topic = topic;
            this.Data = data;
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
