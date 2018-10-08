using System;

namespace Stratis.SmartContracts.Executor.Reflection.Serialization
{
    /// <summary>
    /// Defines the serialization functionality that is exposed to a <see cref="SmartContract"/>.
    /// </summary>
    public class Serializer : ISerializer
    {
        public byte[] Serialize(Address address)
        {
            throw new NotImplementedException();
        }

        public byte[] Serialize(bool b)
        {
            throw new NotImplementedException();
        }

        public byte[] Serialize(int i)
        {
            throw new NotImplementedException();
        }

        public byte[] Serialize(long l)
        {
            throw new NotImplementedException();
        }

        public byte[] Serialize(uint u)
        {
            throw new NotImplementedException();
        }

        public byte[] Serialize(ulong ul)
        {
            throw new NotImplementedException();
        }

        public byte[] Serialize(string s)
        {
            throw new NotImplementedException();
        }

        public byte[] Serialize(Array a)
        {
            throw new NotImplementedException();
        }

        public bool ToBool(byte[] val)
        {
            throw new NotImplementedException();
        }

        public Address ToAddress(byte[] val)
        {
            throw new NotImplementedException();
        }

        public int ToInt32(byte[] val)
        {
            throw new NotImplementedException();
        }

        public uint ToUInt32(byte[] val)
        {
            throw new NotImplementedException();
        }

        public long ToInt64(byte[] val)
        {
            throw new NotImplementedException();
        }

        public ulong ToUInt64(byte[] val)
        {
            throw new NotImplementedException();
        }

        public string ToString(byte[] val)
        {
            throw new NotImplementedException();
        }

        public T[] ToArray<T>(byte[] val)
        {
            throw new NotImplementedException();
        }
    }
}