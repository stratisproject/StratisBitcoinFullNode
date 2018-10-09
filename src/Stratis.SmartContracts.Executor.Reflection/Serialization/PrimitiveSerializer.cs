using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Nethereum.RLP;
using Stratis.SmartContracts.Core;

namespace Stratis.SmartContracts.Executor.Reflection.Serialization
{
    public class PrimitiveSerializer : IPrimitiveSerializer
    {
        private readonly Network network;

        public PrimitiveSerializer(Network network)
        {
            this.network = network;
        }

        public byte[] Serialize(Address address)
        {
            return address.ToUint160(this.network).ToBytes();
        }

        public byte[] Serialize(bool b)
        {
            return BitConverter.GetBytes(b);
        }

        public byte[] Serialize(int i)
        {
            return BitConverter.GetBytes(i);
        }

        public byte[] Serialize(long l)
        {
            return BitConverter.GetBytes(l);
        }

        public byte[] Serialize(uint u)
        {
            return BitConverter.GetBytes(u);
        }

        public byte[] Serialize(ulong ul)
        {
            return BitConverter.GetBytes(ul);
        }

        public byte[] Serialize(string s)
        {
            return Encoding.UTF8.GetBytes(s);
        }

        public bool ToBool(byte[] val)
        {
            return BitConverter.ToBoolean(val);
        }

        public Address ToAddress(byte[] val)
        {
            return new uint160(val).ToAddress(this.network);
        }

        public int ToInt32(byte[] val)
        {
            return BitConverter.ToInt32(val, 0);
        }

        public uint ToUInt32(byte[] val)
        {
            return BitConverter.ToUInt32(val, 0);
        }

        public long ToInt64(byte[] val)
        {
            return BitConverter.ToInt64(val, 0);
        }

        public ulong ToUInt64(byte[] val)
        {
            return BitConverter.ToUInt64(val, 0);
        }

        public string ToString(byte[] val)
        {
            return Encoding.UTF8.GetString(val);
        }
    }
}
