using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NBitcoin;
using Nethereum.RLP;
using Stratis.SmartContracts.CLR.Exceptions;

namespace Stratis.SmartContracts.CLR.Serialization
{
    /// <summary>
    /// This class serializes and deserializes specific data types
    /// when persisting items inside a smart contract.
    /// </summary>
    public class ContractPrimitiveSerializer : IContractPrimitiveSerializer
    {
        private readonly Network network;

        public ContractPrimitiveSerializer(Network network)
        {
            this.network = network;
        }

        public byte[] Serialize(object o)
        {
            if (o is null)
                return null;

            if (o is byte[] bytes)
                return bytes;

            if (o is Array array)
                return Serialize(array);

            if (o is byte b1)
                return new byte[] { b1 };

            if (o is char c)
                return Serialize(c);

            if (o is Address address)
                return Serialize(address);

            if (o is bool b)
                return Serialize(b);

            if (o is int i)
                return Serialize(i);

            if (o is long l)
                return Serialize(l);

            if (o is uint u)
                return Serialize(u);

            if (o is ulong ul)
                return Serialize(ul);

            if (o is string s)
                return Serialize(s);

            if (o.GetType().IsValueType)
                return SerializeStruct(o);
                
            throw new ContractPrimitiveSerializationException(string.Format("{0} is not supported.", o.GetType().Name));
        }

        #region Primitive serialization

        private byte[] Serialize(Address address)
        {
            return address.ToBytes();
        }

        private byte[] Serialize(bool b)
        {
            return BitConverter.GetBytes(b);
        }

        private byte[] Serialize(int i)
        {
            return BitConverter.GetBytes(i);
        }

        private byte[] Serialize(long l)
        {
            return BitConverter.GetBytes(l);
        }

        private byte[] Serialize(uint u)
        {
            return BitConverter.GetBytes(u);
        }

        private byte[] Serialize(ulong ul)
        {
            return BitConverter.GetBytes(ul);
        }

        private byte[] Serialize(char c)
        {
            return BitConverter.GetBytes(c);
        }

        private byte[] Serialize(string s)
        {
            return Encoding.UTF8.GetBytes(s);
        }

        #endregion

        private byte[] SerializeStruct(object o)
        {
            List<byte[]> toEncode = new List<byte[]>(); 

            foreach (FieldInfo field in o.GetType().GetFields())
            {
                object value = field.GetValue(o);
                byte[] serialized = Serialize(value);
                toEncode.Add(RLP.EncodeElement(serialized));
            }

            return RLP.EncodeList(toEncode.ToArray());
        }

        private byte[] Serialize(Array array)
        {
            // Edge case, serializing nonsensical
            if (array is byte[] a)
                return a;

            List<byte[]> toEncode = new List<byte[]>();

            for(int i=0; i< array.Length; i++)
            {
                object value = array.GetValue(i);
                byte[] serialized = Serialize(value);
                toEncode.Add(RLP.EncodeElement(serialized));
            }

            return RLP.EncodeList(toEncode.ToArray());
        }

        public T Deserialize<T>(byte[] stream)
        {
            object deserialized = Deserialize(typeof(T), stream);

            return (T) deserialized;
        }

        public object Deserialize(Type type, byte[] stream)
        {
            if (stream == null || stream.Length == 0)
                return null;

            if (type == typeof(byte[]))
                return stream;

            if (type.IsArray)
                return DeserializeArray(type.GetElementType(), stream);

            if (type == typeof(byte))
                return stream[0];

            if (type == typeof(char))
                return ToChar(stream);

            if (type == typeof(Address))
                return ToAddress(stream);

            if (type == typeof(bool))
                return ToBool(stream);

            if (type == typeof(int))
                return ToInt32(stream);

            if (type == typeof(long))
                return ToInt64(stream);

            if (type == typeof(string))
                return ToString(stream);

            if (type == typeof(uint))
                return ToUInt32(stream);

            if (type == typeof(ulong))
                return ToUInt64(stream);

            if (type.IsValueType)
                return DeserializeStruct(type, stream);
                
            throw new ContractPrimitiveSerializationException(string.Format("{0} is not supported.", type.Name));
        }

        public Address ToAddress(string address)
        {            
            return address.ToAddress(this.network);
        }

        #region Primitive Deserialization

        private bool ToBool(byte[] val)
        {
            return BitConverter.ToBoolean(val);
        }

        private Address ToAddress(byte[] val)
        {
            return val.ToAddress();
        }

        private int ToInt32(byte[] val)
        {
            return BitConverter.ToInt32(val, 0);
        }

        private uint ToUInt32(byte[] val)
        {
            return BitConverter.ToUInt32(val, 0);
        }

        private long ToInt64(byte[] val)
        {
            return BitConverter.ToInt64(val, 0);
        }

        private ulong ToUInt64(byte[] val)
        {
            return BitConverter.ToUInt64(val, 0);
        }

        private char ToChar(byte[] val)
        {
            return BitConverter.ToChar(val, 0);
        }

        private string ToString(byte[] val)
        {
            return Encoding.UTF8.GetString(val);
        }

        #endregion

        private object DeserializeStruct(Type type, byte[] bytes)
        {
            RLPCollection collection = (RLPCollection) RLP.Decode(bytes)[0];

            object ret = Activator.CreateInstance(type);

            FieldInfo[] fields = type.GetFields();

            for (int i = 0; i < fields.Length; i++)
            {
                byte[] fieldBytes = collection[i].RLPData;
                fields[i].SetValue(ret, Deserialize(fields[i].FieldType, fieldBytes));
            }

            return ret;
        }

        private object DeserializeArray(Type elementType, byte[] bytes)
        {
            // Edge case, serializing nonsensical
            if (elementType == typeof(byte))
                return bytes;

            RLPCollection collection = (RLPCollection)RLP.Decode(bytes)[0];

            Array ret = Array.CreateInstance(elementType, collection.Count);

            for(int i=0; i< collection.Count; i++)
            {
                ret.SetValue(Deserialize(elementType, collection[i].RLPData), i);
            }

            return ret;
        }
    }
}
