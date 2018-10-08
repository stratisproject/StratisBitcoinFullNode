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
            if (s == null)
                return null;

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

            (bool success, bool result) = this.TryDeserializeValue<bool>(val);

            return success ? result : default(bool);
        }

        public Address ToAddress(byte[] val)
        {
            if (val == null || val.Length == 0)
                return default(Address);

            (bool success, Address result) = this.TryDeserializeValue<Address>(val);

            return success ? result : default(Address);
        }

        public int ToInt32(byte[] val)
        {
            if (val == null)
                return default(int);

            if (val.Length != 4)
                return default(int);

            (bool success, int result) = this.TryDeserializeValue<int>(val);

            return success ? result : default(int);
        }

        public uint ToUInt32(byte[] val)
        {
            if (val == null)
                return default(uint);

            if (val.Length != 4)
                return default(uint);

            (bool success, uint result) = this.TryDeserializeValue<uint>(val);

            return success ? result : default(uint);
        }

        public long ToInt64(byte[] val)
        {
            if (val == null)
                return default(long);

            if (val.Length != 8)
                return default(long);

            (bool success, long result) = this.TryDeserializeValue<long>(val);

            return success ? result : default(long);
        }

        public ulong ToUInt64(byte[] val)
        {
            if (val == null)
                return default(ulong);

            if (val.Length != 8)
                return default(ulong);

            (bool success, ulong result) = this.TryDeserializeValue<ulong>(val);

            return success ? result : default(ulong);
        }

        public string ToString(byte[] val)
        {
            if (val == null || val.Length < sizeof(char))
                return string.Empty;

            (bool success, string result) = this.TryDeserializeValue<string>(val);

            return success ? result : string.Empty;
        }

        public T[] ToArray<T>(byte[] val)
        {
            if (val == null || val.Length == 0)
                return new T[0];

            (bool success, T[] result) = this.TryDeserializeValue<T[]>(val);

            return success ? result : new T[0];
        }

        private (bool, T) TryDeserializeValue<T>(byte[] val)
        {
            try
            {
                T deserialized = this.primitiveSerializer.Deserialize<T>(val);
                return (true, deserialized);
            }
            catch (Exception)
            {
                return (false, default(T));
            }
        }
    }
}