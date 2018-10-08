using System;

namespace Stratis.SmartContracts.Executor.Reflection.Serialization
{
    /// <summary>
    /// Defines the serialization functionality that is exposed to a <see cref="SmartContract"/>.
    /// </summary>
    public class Serializer : ISerializer
    {
        private readonly IContractPrimitiveSerializer primitiveSerializer;

        public Serializer(IContractPrimitiveSerializer primitiveSerializer)
        {
            this.primitiveSerializer = primitiveSerializer;
        }

        public byte[] Serialize(Address address)
        {
            if (address.Value == null)
                return null;

            return this.primitiveSerializer.Serialize(address);
        }

        public byte[] Serialize(bool b)
        {
            return this.primitiveSerializer.Serialize(b);
        }

        public byte[] Serialize(int i)
        {
            return this.primitiveSerializer.Serialize(i);
        }

        public byte[] Serialize(long l)
        {
            return this.primitiveSerializer.Serialize(l);
        }

        public byte[] Serialize(uint u)
        {
            return this.primitiveSerializer.Serialize(u);
        }

        public byte[] Serialize(ulong ul)
        {
            return this.primitiveSerializer.Serialize(ul);
        }

        public byte[] Serialize(string s)
        {
            return this.primitiveSerializer.Serialize(s);
        }

        public byte[] Serialize(Array a)
        {
            if (a == null)
                return null;

            return this.primitiveSerializer.Serialize(a);
        }

        public bool ToBool(byte[] val)
        {
            if (val == null)
                return default(bool);

            return this.primitiveSerializer.Deserialize<bool>(val);
        }

        public Address ToAddress(byte[] val)
        {
            if (val == null)
                return default(Address);

            return this.primitiveSerializer.Deserialize<Address>(val);
        }

        public int ToInt32(byte[] val)
        {
            if (val == null)
                return default(int);

            if (val.Length != 4)
                return default(int);

            return this.primitiveSerializer.Deserialize<int>(val);
        }

        public uint ToUInt32(byte[] val)
        {
            if (val == null)
                return default(uint);

            if (val.Length != 4)
                return default(uint);

            return this.primitiveSerializer.Deserialize<uint>(val);
        }

        public long ToInt64(byte[] val)
        {
            if (val == null)
                return default(long);

            if (val.Length != 8)
                return default(long);

            return this.primitiveSerializer.Deserialize<long>(val);
        }

        public ulong ToUInt64(byte[] val)
        {
            if (val == null)
                return default(ulong);

            if (val.Length != 8)
                return default(ulong);

            return this.primitiveSerializer.Deserialize<ulong>(val);
        }

        public string ToString(byte[] val)
        {
            if (val == null)
                return string.Empty;

            return this.primitiveSerializer.Deserialize<string>(val);
        }

        public T[] ToArray<T>(byte[] val)
        {
            if (val == null)
                return new T[0];

            return this.primitiveSerializer.Deserialize<T[]>(val);
        }
    }
}