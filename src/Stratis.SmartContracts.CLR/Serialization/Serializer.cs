using System;
using System.Collections.Generic;
using System.Reflection;
using Nethereum.RLP;

namespace Stratis.SmartContracts.CLR.Serialization
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

        public byte[] Serialize(char c)
        {
            return this.primitiveSerializer.Serialize(c);
        }

        public byte[] Serialize(Address address)
        {
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

        public byte[] Serialize<T>(T s) where T : struct
        {
            var toEncode = new List<byte[]>();

            foreach (FieldInfo field in s.GetType().GetFields())
            {
                object value = field.GetValue(s);

                byte[] serialized = value != null 
                    ? this.primitiveSerializer.Serialize(value) ?? new byte[0]
                    : new byte[0];

                toEncode.Add(RLP.EncodeElement(serialized));
            }

            return RLP.EncodeList(toEncode.ToArray());
        }

        public bool ToBool(byte[] val)
        {
            if (val == null || val.Length == 0)
                return default(bool);

            (bool success, bool result) = this.TryDeserializeValue<bool>(val);

            return success ? result : default(bool);
        }

        public Address ToAddress(byte[] val)
        {
            if (val == null || val.Length != Address.Width)
                return Address.Zero;

            (bool success, Address address) = this.TryDeserializeValue<Address>(val);

            return success
                ? address
                : Address.Zero;
        }

        public Address ToAddress(string val)
        {
            if (string.IsNullOrWhiteSpace(val))
                return Address.Zero;

            try
            {
                return this.primitiveSerializer.ToAddress(val);
            }
            catch (Exception)
            {
                return Address.Zero;
            }
        }

        public int ToInt32(byte[] val)
        {
            if (val == null)
                return default(int);

            if (val.Length < 4)
                return default(int);

            (bool success, int result) = this.TryDeserializeValue<int>(val);

            return success ? result : default(int);
        }

        public uint ToUInt32(byte[] val)
        {
            if (val == null)
                return default(uint);

            if (val.Length < 4)
                return default(uint);

            (bool success, uint result) = this.TryDeserializeValue<uint>(val);

            return success ? result : default(uint);
        }

        public long ToInt64(byte[] val)
        {
            if (val == null)
                return default(long);

            if (val.Length < 8)
                return default(long);

            (bool success, long result) = this.TryDeserializeValue<long>(val);

            return success ? result : default(long);
        }

        public ulong ToUInt64(byte[] val)
        {
            if (val == null)
                return default(ulong);

            if (val.Length < 8)
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

        public char ToChar(byte[] val)
        {
            if (val == null || val.Length < sizeof(char))
                return default(char);

            (bool success, char result) = this.TryDeserializeValue<char>(val);

            return success ? result : default(char);
        }

        public T[] ToArray<T>(byte[] val)
        {
            if (val == null || val.Length == 0)
                return new T[0];

            (bool success, T[] result) = this.TryDeserializeValue<T[]>(val);

            return success ? result : new T[0];
        }

        public T ToStruct<T>(byte[] val) where T: struct
        {
            if (val == null || val.Length == 0)
                return default(T);

            try
            {
                // DeserializeStruct uses ContractPrimitiveSerializer, which can throw exceptions
                // eg. if a field deserializes incorrectly.
                T result = this.DeserializeStruct<T>(val);

                return result;
            }
            catch (Exception)
            {
                return default(T);
            }
        }

        private T DeserializeStruct<T>(byte[] bytes) where T : struct
        {
            RLPCollection collection = (RLPCollection)RLP.Decode(bytes)[0];

            Type type = typeof(T);

            // This needs to be a boxed struct or we won't be able to set the fields with reflection.
            object instance = Activator.CreateInstance(type);
            
            FieldInfo[] fields = type.GetFields();

            for (int i = 0; i < fields.Length; i++)
            {
                byte[] fieldBytes = collection[i].RLPData;
                Type fieldType = fields[i].FieldType;
                object fieldValue = this.primitiveSerializer.Deserialize(fieldType, fieldBytes);
                fields[i].SetValue(instance, fieldValue);
            }

            return (T) instance;
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