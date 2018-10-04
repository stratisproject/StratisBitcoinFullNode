﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NBitcoin;
using Nethereum.RLP;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Executor.Reflection.Exceptions;

namespace Stratis.SmartContracts.Executor.Reflection.Serialization
{
    /// <summary>
    /// This class serializes and deserializes specific data types
    /// when persisting items inside a smart contract.
    /// </summary>
    public class ContractPrimitiveSerializer : IContractPrimitiveSerializer, ISerializer
    {
        private static readonly byte[] DefaultValue = new byte[0];
        private readonly Network network;

        public ContractPrimitiveSerializer(Network network)
        {
            this.network = network;
        }

        public byte[] Serialize(object o)
        {
            if (o is null)
                return new byte[0];

            if (o is byte[] bytes)
                return bytes;

            if (o is Array array)
                return SerializeArray(array);

            if (o is byte b1)
                return new byte[] { b1 };

            if (o is char c)
                return Serialize(c.ToString());

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

        public byte[] Serialize(Address address)
        {
            if (address == default(Address))
                return DefaultValue;

            return address.ToUint160(this.network).ToBytes();
        }

        public byte[] Serialize(bool b)
        {
            if (b == default(bool))
                return DefaultValue;

            return BitConverter.GetBytes(b);
        }

        public byte[] Serialize(int i)
        {
            if (i == default(int))
                return DefaultValue;

            return BitConverter.GetBytes(i);
        }

        public byte[] Serialize(long l)
        {
            if (l == default(long))
                return DefaultValue;

            return BitConverter.GetBytes(l);
        }

        public byte[] Serialize(uint u)
        {
            if (u == default(uint))
                return DefaultValue;

            return BitConverter.GetBytes(u);
        }

        public byte[] Serialize(ulong ul)
        {
            if (ul == default(ulong))
                return DefaultValue;

            return BitConverter.GetBytes(ul);
        }

        public byte[] Serialize(string s)
        {
            if (s == default(string))
                return DefaultValue;

            return Encoding.UTF8.GetBytes(s);
        }

        #endregion

        public byte[] SerializeStruct(object o)
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

        public byte[] SerializeArray(Array array)
        {
            List<byte[]> toEncode = new List<byte[]>();

            for(int i=0; i< array.Length; i++)
            {
                object value = array.GetValue(i);
                byte[] serialized = Serialize(value);
                toEncode.Add(RLP.EncodeElement(serialized));
            }

            return RLP.EncodeList(toEncode.ToArray());
        }

        // Purely kept in for some tests at the moment.
        public T Deserialize<T>(byte[] stream)
        {
            object deserialized = Deserialize(typeof(T), stream);
            if (deserialized == null)
                return default(T);

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
                return ToString(stream)[0];

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

        #region Primitive Deserialization

        public bool ToBool(byte[] val)
        {
            if (val.Length == 0)
                return default(bool);

            return BitConverter.ToBoolean(val);
        }

        public char ToChar(byte[] val)
        {
            if (val.Length == 0)
                return default(char);

            return ToString(val)[0];
        }

        public Address ToAddress(byte[] val)
        {
            if (val.Length == 0)
                return default(Address);

            return new uint160(val).ToAddress(this.network);
        }

        public int ToInt32(byte[] val)
        {
            if (val.Length == 0)
                return default(int);

            return BitConverter.ToInt32(val, 0);
        }

        public uint ToUInt32(byte[] val)
        {
            if (val.Length == 0)
                return default(uint);

            return BitConverter.ToUInt32(val, 0);
        }

        public long ToInt64(byte[] val)
        {
            if (val.Length == 0)
                return default(long);

            return BitConverter.ToInt64(val, 0);
        }

        public ulong ToUInt64(byte[] val)
        {
            if (val.Length == 0)
                return default(ulong);

            return BitConverter.ToUInt64(val, 0);
        }

        public string ToString(byte[] val)
        {
            if (val.Length == 0)
                return default(string);

            return Encoding.UTF8.GetString(val);
        }

        public T ToStruct<T>(byte[] val) where T : struct
        {
            return (T) DeserializeStruct(typeof(T), val);
        }

        public T[] ToArray<T>(byte[] val)
        {
            return (T[]) DeserializeArray(typeof(T), val);
        }
        #endregion

        private object DeserializeStruct(Type type, byte[] bytes)
        {
            RLPCollection collection = (RLPCollection) RLP.Decode(bytes)[0];

            var ret = Activator.CreateInstance(type);

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
            RLPCollection collection = (RLPCollection)RLP.Decode(bytes)[0];

            var ret = Array.CreateInstance(elementType, collection.Count);

            for(int i=0; i< collection.Count; i++)
            {
                ret.SetValue(Deserialize(elementType, collection[i].RLPData), i);
            }

            return ret;
        }
    }
}
